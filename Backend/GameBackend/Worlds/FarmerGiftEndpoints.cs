/// <summary>
/// Gift offers between farmers standing next to each other, modelled on FarmerTeam proposals:
/// one outgoing offer per sender, the receiver answers through a dialog, the sender waits and
/// may cancel (PendingProposalDialog). Offers live in memory with the rest of world presence.
/// </summary>
public static partial class WorldSessionEndpoints
{
    private const string GiftPending = "pending";
    private const string GiftAccepting = "accepting";
    private const string GiftAccepted = "accepted";
    private const string GiftRejected = "rejected";
    private const string GiftExpired = "expired";
    private static readonly TimeSpan GiftAnswerTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan GiftResultKeep = TimeSpan.FromSeconds(60);
    /// <summary>Tiles between the two farmers; a little over the 2.4 tile reach for latency.</summary>
    private const float GiftReach = 3.5f;

    // Keyed by sender; guarded by SyncLock.
    private static readonly Dictionary<Guid, FarmerGiftOffer> GiftOffers = new();

    private sealed class FarmerGiftOffer
    {
        public Guid Id { get; init; }
        public Guid SenderId { get; init; }
        public Guid ReceiverId { get; init; }
        public string SenderName { get; init; } = "";
        public string ReceiverName { get; init; } = "";
        public int Slot { get; init; }
        public string ItemId { get; init; } = "";
        public DateTime CreatedUtc { get; init; }
        public string Status { get; set; } = GiftPending;
        public string? Message { get; set; }
        public DateTime FinishedUtc { get; set; }
        public bool IsFinal => Status is GiftAccepted or GiftRejected or GiftExpired;
    }

    private static void MapFarmerGifts(this WebApplication app)
    {
        // Farmer.checkAction → GiftPlayerItem question → team.SendProposal(..., Gift, item).
        app.MapPost("/api/world/gift/offer", async (HttpRequest request, FarmerGiftOfferRequest input,
            GameDb db, IItemCatalog catalog) =>
        {
            var user = await AuthTokens.ResolveUser(request, db);
            if (user == null) return Results.Unauthorized();
            if (input.ReceiverId == Guid.Empty || input.ReceiverId == user.Id)
                return Results.BadRequest(new ApiError("Không tìm thấy người nhận."));

            var itemError = await GameStateEndpoints.FarmerGiftOfferError(db, catalog, user.Id, input.Slot, input.ItemId);
            if (itemError != null) return Results.BadRequest(new ApiError(itemError));

            var now = DateTime.UtcNow;
            lock (SyncLock)
            {
                ExpireGifts(now);
                if (!ActivePlayers.TryGetValue(user.Id, out var sender) ||
                    !ActivePlayers.TryGetValue(input.ReceiverId, out var receiver) ||
                    receiver.LastSeenUtc < now - InactivityTimeout || IsPrivateLocation(sender.Location) ||
                    !string.Equals(sender.Location, receiver.Location, StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest(new ApiError("Người nhận không còn ở gần bạn."));
                float dx = sender.X - receiver.X, dy = sender.Y - receiver.Y;
                if (dx * dx + dy * dy > GiftReach * GiftReach)
                    return Results.BadRequest(new ApiError("Hãy đứng gần " + receiver.Username + " hơn."));
                if (receiver.InCutscene)
                    return Results.BadRequest(new ApiError(receiver.Username + " đang bận."));
                if (GiftOffers.TryGetValue(user.Id, out var existing))
                {
                    if (!existing.IsFinal) return Results.Conflict(new ApiError("Bạn đang chờ một lời tặng quà khác."));
                    GiftOffers.Remove(user.Id);
                }
                // FarmerTeam: a farmer already answering a proposal is busy (Proposal_PlayerBusy).
                if (GiftOffers.Values.Any(o => o.ReceiverId == input.ReceiverId && !o.IsFinal))
                    return Results.Conflict(new ApiError(receiver.Username + " đang bận."));

                var offer = new FarmerGiftOffer
                {
                    Id = Guid.NewGuid(),
                    SenderId = user.Id,
                    ReceiverId = input.ReceiverId,
                    SenderName = user.Username,
                    ReceiverName = receiver.Username,
                    Slot = input.Slot,
                    ItemId = input.ItemId!,
                    CreatedUtc = now
                };
                GiftOffers[user.Id] = offer;
                return Results.Ok(new FarmerGiftOfferResult(offer.Id));
            }
        });

        // PendingProposalDialog → the receiver's yes/no; accepting moves the item.
        app.MapPost("/api/world/gift/respond", async (HttpRequest request, FarmerGiftRespondRequest input,
            GameDb db, IItemCatalog catalog) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();

            FarmerGiftOffer? offer;
            lock (SyncLock)
            {
                ExpireGifts(DateTime.UtcNow);
                offer = GiftOffers.Values.FirstOrDefault(o => o.Id == input.ProposalId && o.ReceiverId == userId.Value);
                if (offer == null || offer.Status != GiftPending)
                    return Results.BadRequest(new ApiError("Lời tặng quà này không còn hiệu lực."));
                if (!input.Accept)
                {
                    Finish(offer, GiftRejected, offer.ReceiverName + " đã từ chối món quà.");
                    return Results.Ok(new { status = GiftRejected });
                }
                // Holds the offer while the database runs, so it cannot be canceled half-way.
                offer.Status = GiftAccepting;
            }

            string? error;
            GameSnapshot? snapshot;
            try
            {
                (error, snapshot) = await GameStateEndpoints.GiveItemToFarmer(
                    db, catalog, offer.SenderId, offer.ReceiverId, offer.Slot, offer.ItemId);
            }
            catch
            {
                // Never leave the sender waiting on an offer stuck in "accepting".
                lock (SyncLock) Finish(offer, GiftRejected, "Không tặng được quà.");
                throw;
            }
            lock (SyncLock)
            {
                if (error != null)
                {
                    Finish(offer, GiftRejected, "Không tặng được quà: " + error);
                    return Results.BadRequest(new ApiError(error));
                }
                Finish(offer, GiftAccepted, offer.ReceiverName + " đã nhận món quà.");
            }
            return Results.Ok(snapshot);
        });

        // PendingProposalDialog.cancelProposal.
        app.MapPost("/api/world/gift/cancel", async (HttpRequest request, GameDb db) =>
        {
            var userId = await AuthTokens.ResolveUserId(request, db);
            if (userId == null) return Results.Unauthorized();
            lock (SyncLock)
            {
                if (GiftOffers.TryGetValue(userId.Value, out var offer) && offer.Status == GiftPending)
                {
                    GiftOffers.Remove(userId.Value);
                    return Results.Ok(new { status = "canceled" });
                }
            }
            return Results.Conflict(new ApiError("Món quà đã được trả lời."));
        });
    }

    /// <summary>
    /// The offer waiting for <paramref name="userId"/> and this farmer's own offer. A final answer
    /// is handed to the sender once and then forgotten. Call under SyncLock.
    /// </summary>
    private static (FarmerGiftView? Incoming, FarmerGiftView? Outgoing) TakeGiftViews(Guid userId, DateTime now)
    {
        ExpireGifts(now);
        var incoming = GiftOffers.Values.FirstOrDefault(o => o.ReceiverId == userId && o.Status == GiftPending);
        FarmerGiftView? outgoingView = null;
        if (GiftOffers.TryGetValue(userId, out var outgoing))
        {
            outgoingView = new FarmerGiftView(outgoing.Id, outgoing.ReceiverId, outgoing.ReceiverName,
                outgoing.ItemId, outgoing.Status == GiftAccepting ? GiftPending : outgoing.Status, outgoing.Message);
            if (outgoing.IsFinal) GiftOffers.Remove(userId);
        }
        var incomingView = incoming == null ? null : new FarmerGiftView(incoming.Id, incoming.SenderId,
            incoming.SenderName, incoming.ItemId, incoming.Status, null);
        return (incomingView, outgoingView);
    }

    private static void ExpireGifts(DateTime now)
    {
        var cutoff = now - InactivityTimeout;
        foreach (var (senderId, offer) in GiftOffers.ToList())
        {
            if (offer.IsFinal)
            {
                // The sender left before reading the answer.
                if (now - offer.FinishedUtc > GiftResultKeep) GiftOffers.Remove(senderId);
                continue;
            }
            if (offer.Status != GiftPending) continue;
            bool senderGone = !ActivePlayers.TryGetValue(offer.SenderId, out var sender) || sender.LastSeenUtc < cutoff;
            bool receiverGone = !ActivePlayers.TryGetValue(offer.ReceiverId, out var receiver) || receiver.LastSeenUtc < cutoff;
            if (senderGone) GiftOffers.Remove(senderId);
            else if (receiverGone) Finish(offer, GiftExpired, offer.ReceiverName + " đã rời đi.");
            else if (now - offer.CreatedUtc > GiftAnswerTimeout)
                Finish(offer, GiftExpired, offer.ReceiverName + " không trả lời.");
        }
    }

    private static void Finish(FarmerGiftOffer offer, string status, string message)
    {
        offer.Status = status;
        offer.Message = message;
        offer.FinishedUtc = DateTime.UtcNow;
    }
}

public record FarmerGiftOfferRequest(Guid ReceiverId, int Slot, string? ItemId);
public record FarmerGiftOfferResult(Guid ProposalId);
public record FarmerGiftRespondRequest(Guid ProposalId, bool Accept);

/// <summary>An offer as one farmer sees it; <c>OtherUserId</c>/<c>OtherName</c> is the other farmer.</summary>
public record FarmerGiftView(Guid Id, Guid OtherUserId, string OtherName, string ItemId, string Status, string? Message);
