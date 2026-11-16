using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gifts between farmers, after FarmerTeam proposals (ProposalType.Gift):
/// the giver confirms "Give X to Y?", then waits with a cancel button (PendingProposalDialog);
/// the receiver is asked to accept or decline; the server moves the item only on accept.
/// Offers and answers travel with world presence.
/// </summary>
internal sealed class WorldPlayerGifts : MonoBehaviour
{
    private const string Pending = "pending";
    private const string Accepted = "accepted";
    /// <summary>Presence polls without our offer before it is treated as lost (server restart).</summary>
    private const int LostOfferPolls = 30;

    internal static WorldPlayerGifts Instance { get; private set; }

    // Our offer: "" while the request is in flight, then the server id until it is answered.
    private string offerId;
    private string offerReceiver;
    private string offerItemName;
    private bool waitingDialogShown;
    private bool canceling;
    private int pollsWithoutOffer;

    // The incoming offer currently on screen.
    private string promptedId;
    private readonly HashSet<string> promptedOffers = new(StringComparer.Ordinal);

    private void Awake() => Instance = this;

    private void OnEnable() => NpcConversation.Closed += OnDialogClosed;

    private void OnDisable() => NpcConversation.Closed -= OnDialogClosed;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private static StartupApiClient Api => WorldMultiplayerManager.Instance?.Api;
    private static OnlineGameState Online => WorldMultiplayerManager.Instance?.LocalState;

    internal void ResetState()
    {
        offerId = null;
        waitingDialogShown = false;
        canceling = false;
        promptedId = null;
        promptedOffers.Clear();
    }

    /// <summary>
    /// Farmer.checkAction with an object in hand: asks to give it to the farmer in front.
    /// True when another farmer was the target, so the click does nothing else.
    /// </summary>
    internal static bool TryOffer(Vector2? pointer, CharacterMotor motor, PlayerInventory inventory)
    {
        var self = Instance;
        var world = WorldMultiplayerManager.Instance;
        if (self == null || world == null || Api == null || !Api.IsSignedIn || Online == null ||
            motor == null || inventory == null) return false;
        InventorySlot selected = inventory.SelectedItem;
        if (selected == null || selected.amount < 1 || selected.itemData == null ||
            PlayerAppearance.IsTool(selected)) return false;
        if (!world.Remotes.TryFindNear(pointer, motor.StandingPosition, motor.Facing,
                out string userId, out string name, out Vector3 feet)) return false;

        motor.SetMovement(Vector2.zero);
        motor.FaceTowards((Vector2)feet - (Vector2)motor.StandingPosition);
        if (self.offerId != null)
        {
            NpcConversation.ShowNotice("Bạn đang chờ " + self.offerReceiver + " trả lời.");
            return true;
        }

        int slot = inventory.SelectedIndex;
        string itemId = selected.itemData.itemId;
        string itemName = selected.itemData.displayName;
        NpcConversation.ShowChoices("Tặng " + itemName + " cho " + name + "?", new (string, Action)[]
        {
            ("Có", () => self.SendOffer(userId, name, slot, itemId, itemName)),
            ("Không", () => { })
        });
        return true;
    }

    private void SendOffer(string userId, string name, int slot, string itemId, string itemName)
    {
        var api = Api;
        if (api == null) return;
        offerId = "";
        offerReceiver = name;
        offerItemName = itemName;
        canceling = false;
        pollsWithoutOffer = 0;
        StartCoroutine(api.OfferFarmerGift(userId, slot, itemId, (id, error) =>
        {
            if (error != null || string.IsNullOrEmpty(id))
            {
                offerId = null;
                NpcConversation.ShowNotice(error ?? "Không gửi được quà.");
                return;
            }
            offerId = id;
            ShowWaiting();
        }));
    }

    private void ShowWaiting()
    {
        NpcConversation.ShowChoices("Đang chờ " + offerReceiver + " nhận " + offerItemName + "...",
            new (string, Action)[] { ("Hủy", CancelOffer) });
        waitingDialogShown = true;
    }

    /// <summary>PendingProposalDialog.cancelProposal; the answer may already be on its way.</summary>
    private void CancelOffer()
    {
        var api = Api;
        if (string.IsNullOrEmpty(offerId) || canceling || api == null) return;
        canceling = true;
        string id = offerId;
        StartCoroutine(api.CancelFarmerGift(error =>
        {
            canceling = false;
            if (offerId != id) return;
            // Too late to cancel: the receiver answered and presence will report it.
            if (error != null) return;
            offerId = null;
            NpcConversation.ShowNotice("Đã hủy tặng quà.");
        }));
    }

    private void OnDialogClosed()
    {
        // Closing the waiting dialog (Esc or "Hủy") cancels, like the proposal dialog.
        if (waitingDialogShown)
        {
            waitingDialogShown = false;
            CancelOffer();
        }
        // A choice closes the dialog before it runs, so decide on the next frame.
        if (promptedId != null) StartCoroutine(DeclineIfUnanswered(promptedId));
    }

    private IEnumerator DeclineIfUnanswered(string id)
    {
        yield return null;
        if (promptedId == id) Answer(id, false, null);
    }

    internal void OnPresence(StartupApiClient.WorldPresenceResponse response)
    {
        HandleOutgoing(response.outgoingGift);
        HandleIncoming(response.incomingGift);
    }

    private void HandleOutgoing(StartupApiClient.FarmerGiftDto offer)
    {
        if (string.IsNullOrEmpty(offerId)) return;
        bool ours = offer != null && offer.IsPresent && offer.id == offerId;
        if (!ours)
        {
            if (++pollsWithoutOffer < LostOfferPolls) return;
            CloseWaiting();
            offerId = null;
            NpcConversation.ShowNotice("Lời tặng quà đã hết hạn.");
            return;
        }
        pollsWithoutOffer = 0;
        if (offer.status == Pending) return;

        offerId = null;
        CloseWaiting();
        bool accepted = offer.status == Accepted;
        if (accepted)
        {
            GameAudio.Play("give_gift");
            // The item left our bag on the server.
            Online?.Reload();
        }
        NpcConversation.ShowNotice(string.IsNullOrEmpty(offer.message)
            ? (accepted ? offer.otherName + " đã nhận món quà." : offer.otherName + " không nhận món quà.")
            : offer.message);
    }

    private void CloseWaiting()
    {
        if (!waitingDialogShown) return;
        waitingDialogShown = false;
        NpcConversation.Close();
    }

    private void HandleIncoming(StartupApiClient.FarmerGiftDto offer)
    {
        bool present = offer != null && offer.IsPresent && offer.status == Pending;
        // The giver canceled or the offer expired while it was on screen.
        if (promptedId != null && (!present || offer.id != promptedId))
        {
            promptedId = null;
            if (NpcConversation.IsOpen) NpcConversation.Close();
            NpcConversation.ShowNotice("Lời tặng quà đã bị hủy.");
        }
        if (!present || offer.id == promptedId || promptedOffers.Contains(offer.id) || !CanPrompt()) return;

        promptedOffers.Add(offer.id);
        promptedId = offer.id;
        string id = offer.id;
        string itemName = Online?.ResolveItem(offer.itemId)?.displayName ?? offer.itemId;
        NpcConversation.ShowChoices(offer.otherName + " muốn tặng bạn " + itemName + ".", new (string, Action)[]
        {
            ("Nhận", () => Answer(id, true, itemName)),
            ("Từ chối", () => Answer(id, false, itemName))
        });
    }

    /// <summary>FarmerTeam.handleIncomingProposal: busy farmers are asked later.</summary>
    private static bool CanPrompt()
    {
        var online = Online;
        return online != null && !GameUi.IsMenuOrInventoryOpen &&
            !online.IsChestInteractionOpen && !WorldMultiplayerManager.IsLocalCutscenePlaying &&
            LocationManager.Instance?.IsWarping != true;
    }

    private void Answer(string id, bool accept, string itemName)
    {
        if (promptedId == id) promptedId = null;
        var api = Api;
        if (api == null) return;
        StartCoroutine(api.RespondFarmerGift(id, accept, (snapshot, error) =>
        {
            if (!accept) return;
            if (error != null)
            {
                NpcConversation.ShowNotice(error);
                return;
            }
            Online?.ApplyAdminSnapshot(snapshot, null);
            GameAudio.Play("coin");
            NpcConversation.ShowNotice("Bạn đã nhận " + itemName + ".");
        }));
    }
}
