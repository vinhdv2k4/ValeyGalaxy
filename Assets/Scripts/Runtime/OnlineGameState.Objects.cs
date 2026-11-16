using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Placed object requests: machines, gates, bombs and staircases.</summary>
public sealed partial class OnlineGameState
{
    internal void ToggleGate(string objectId, string itemId, Action<string> done) => ObjectAction("toggle", objectId, itemId, done);

    internal void ThrowBomb(string bomb, string location, int x, int y, Action<string> done) =>
        ObjectAt("bomb", bomb, location, x, y, done);

    internal void UseStaircase(string location, Action<string> done) =>
        ObjectAt("staircase", "big_craftable_71", location, 0, 0, done);

    private void ObjectAt(string action, string itemId, string location, int x, int y, Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Bạn cần đăng nhập."); return; }
        StartCoroutine(api.ObjectAt(action, itemId, location, x, y, PlacedObjectsView.MinuteOfDay(), (snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            done(error);
        }));
    }

    internal void PlaceObject(string itemId, string location, int x, int y, string variant, Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Bạn cần đăng nhập."); return; }
        StartCoroutine(api.PlaceObject(itemId, location, x, y, variant, PlacedObjectsView.MinuteOfDay(), (snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            done(error);
        }));
    }

    internal void PickUpObject(string objectId, string itemId, Action<string> done) => ObjectAction("pickup", objectId, itemId, done);
    internal void LoadMachine(string objectId, string itemId, Action<string> done) => ObjectAction("load", objectId, itemId, done);
    internal void CollectFromMachine(string objectId, string itemId, Action<string> done) => ObjectAction("collect", objectId, itemId, done);

    private void ObjectAction(string action, string objectId, string itemId, Action<string> done)
    {
        if (api == null || Snapshot == null) { done("Bạn cần đăng nhập."); return; }
        StartCoroutine(api.ObjectAction(action, objectId, itemId, PlacedObjectsView.MinuteOfDay(), (snapshot, error) =>
        {
            if (error == null) ApplyResult(snapshot, null);
            done(error);
        }));
    }
}
