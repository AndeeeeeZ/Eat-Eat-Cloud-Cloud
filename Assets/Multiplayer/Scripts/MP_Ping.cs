using System;
using PurrNet;
using PurrNet.Transports;
using UnityEngine;

public class MP_Ping : NetworkBehaviour
{
    [SerializeField, Min(0.1f)] private float pingInterval = 1f;
    [SerializeField, Min(0.1f)] private float timeout = 3f;

    // Local client round-trip time including RPC scheduling, null until a valid reply
    public int? PingMilliseconds { get; private set; }
    public bool HasTimedOut { get; private set; }
    public bool IsConnected => IsSpawned(false) && isClient;
    public event Action<int?> OnPingChanged;

    private uint sequence;
    private bool awaitingReply;
    private double sentAt;
    private double nextPingAt;

    private void Update()
    {
        if (!IsConnected)
            return;

        double now = Time.realtimeSinceStartupAsDouble;
        if (awaitingReply)
        {
            if (now - sentAt < Mathf.Max(0.1f, timeout))
                return;

            awaitingReply = false;
            HasTimedOut = true;
            SetPing(null);
        }

        if (now < nextPingAt)
            return;

        sequence++;
        sentAt = now;
        nextPingAt = now + Mathf.Max(0.1f, pingInterval);
        awaitingReply = true;
        SendPing(sequence);
    }

    [ServerRpc(channel: Channel.Unreliable, requireOwnership: false)]
    private void SendPing(uint requestSequence, RPCInfo info = default)
    {
        // PurrNet supplies the sender identity so clients cannot choose another recipient
        ReceivePong(info.sender, requestSequence);
    }

    [TargetRpc(channel: Channel.Unreliable)]
    private void ReceivePong(PlayerID target, uint requestSequence)
    {
        if (!IsConnected || !awaitingReply || requestSequence != sequence)
            return;

        double elapsed = Time.realtimeSinceStartupAsDouble - sentAt;
        awaitingReply = false;
        HasTimedOut = elapsed >= Mathf.Max(0.1f, timeout);
        SetPing(HasTimedOut ? (int?)null : Math.Max(0, (int)Math.Round(elapsed * 1000.0)));
    }

    private void SetPing(int? milliseconds)
    {
        if (PingMilliseconds == milliseconds)
            return;

        PingMilliseconds = milliseconds;
        OnPingChanged?.Invoke(milliseconds);
    }

    protected override void OnSpawned(bool asServer)
    {
        base.OnSpawned(asServer);
        if (!asServer)
            ResetPing();
    }

    protected override void OnDespawned(bool asServer)
    {
        base.OnDespawned(asServer);
        if (!asServer)
            ResetPing();
    }

    private void ResetPing()
    {
        // Keep the sequence increasing to reject replies from a previous spawn
        awaitingReply = false;
        nextPingAt = 0;
        HasTimedOut = false;
        SetPing(null);
    }
}
