using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Ranch requests: buildings and animal care.</summary>
public sealed partial class OnlineGameState
{
    internal void OrderRanchHome(string kind, int level, string buildingId, int x, int y, Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Bạn cần đăng nhập."); return; }
        StartCoroutine(api.OrderRanchHome(kind, level, buildingId, x, y, (snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            done(error);
        }));
    }

    internal void DemolishRanchHome(string buildingId, Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Bạn cần đăng nhập."); return; }
        StartCoroutine(api.DemolishRanchHome(buildingId, (snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            done(error);
        }));
    }

    internal void PaintRanchHome(string buildingId, OnlineRanchPaint[] regions, Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Bạn cần đăng nhập."); return; }
        StartCoroutine(api.PaintRanchHome(buildingId, regions, (snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            done(error);
        }));
    }

    internal void MoveRanchHome(string buildingId, int x, int y, Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Bạn cần đăng nhập."); return; }
        StartCoroutine(api.MoveRanchHome(buildingId, x, y, (snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            done(error);
        }));
    }

    internal void PetRanchAnimal(string animalId, Action<string> done) => RanchCare("pet", animalId, null, 0, null, done);
    internal void HarvestRanchAnimal(string animalId, Action<string> done) => RanchCare("harvest", animalId, null, 0, null, done);
    internal void PutHayInTrough(string buildingId, int trough, Action<string> done) => RanchCare("hay", null, buildingId, trough, null, done);
    internal void ToggleAnimalDoor(string buildingId, Action<string> done) => RanchCare("door", null, buildingId, 0, null, done);
    internal void CollectRanchProduct(string productId, Action<string> done) => RanchCare("collect", null, null, 0, productId, done);
    internal void TakeHayFromHopper(string buildingId, Action<string> done) => RanchCare("hopper", null, buildingId, 0, null, done);
    internal void GrazeAnimal(string animalId, int tileX, int tileY, Action<string> done) => RanchField("graze", animalId, tileX, tileY, done);
    internal void DigTruffle(string animalId, int tileX, int tileY, Action<string> done) => RanchField("dig", animalId, tileX, tileY, done);

    private void RanchField(string action, string animalId, int tileX, int tileY, Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Bạn cần đăng nhập."); return; }
        StartCoroutine(api.RanchField(action, animalId, tileX, tileY, (snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            done(error);
        }));
    }

    private void RanchCare(string action, string animalId, string buildingId, int trough, string productId, Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Bạn cần đăng nhập."); return; }
        StartCoroutine(api.RanchCare(action, animalId, buildingId, trough, productId, (snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            done(error);
        }));
    }

    internal void BuyRanchAnimal(string species, string name, string home, Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Bạn cần đăng nhập."); return; }
        StartCoroutine(api.BuyRanchAnimal(species, name, home, (snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            done(error);
        }));
    }
}
