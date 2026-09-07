using AdminToys;
using LabApi.Features.Wrappers;
using Mirror;
using UnityEngine;

namespace ProjectMER.Features.ClientSideToys;

public abstract class ClientSideAdminToy
{
    public Vector3 LocalPosition { get; set; }
    public Quaternion LocalRotation { get; set; }
    public Vector3 Scale { get; set; }
    public byte MovementSmoothing { get; set; }
    public bool IsStatic { get; set; }
    public uint ParentNetId { get; set; }
    public uint NetId { get; set; }
    protected abstract uint AssetID { get; }
    
    private bool _spawnMessageSet = false;
    private SpawnMessage _spawnMessage;

    public ClientSideAdminToy()
    {
        
    }

    public ClientSideAdminToy(AdminToyBase adminToy)
    {
        LocalPosition = adminToy.Position;
        LocalRotation = adminToy.Rotation;
        Scale = adminToy.Scale;
        MovementSmoothing =  adminToy.MovementSmoothing;
        IsStatic = adminToy.IsStatic;
        SetParent(adminToy.transform.parent);
        NetId = adminToy.netIdentity.netId;
    }
    
    public void SetParent(Transform parent)
    {
        if (parent == null)
            return;
        if (parent.TryGetComponent(out NetworkIdentity networkIdentity))
        {
            ParentNetId = networkIdentity.netId;
        }
    }

    public SpawnMessage GetSpawnMessage(NetworkWriter writer)
    {
        if (_spawnMessageSet)
            return _spawnMessage;
        
        Compression.CompressVarUInt(writer, 1UL);

        var headerPos = writer.Position;
        writer.WriteByte(0);
        var contentPos = writer.Position;
        WriteSyncObjects(writer);
        WriteSyncVars(writer);
        WriteOnSerialize(writer);
        var endPos = writer.Position;
        writer.Position = headerPos;
        writer.WriteByte((byte)((endPos - contentPos) & 0xFF));
        writer.Position = endPos;
        
        _spawnMessage = new SpawnMessage
        {
            assetId = AssetID,
            position = LocalPosition,
            rotation = LocalRotation,
            scale = Scale,
            netId = NetId,
            payload = new ArraySegment<byte>(writer.ToArray()),
        };
        _spawnMessageSet = true;
        return _spawnMessage;
    }
    
    public void Spawn(NetworkConnection conn)
    {
        using var writer = NetworkWriterPool.Get();
        conn.Send(GetSpawnMessage(writer));
    }
    
    public void Destroy(NetworkConnection conn)
    {
        conn.Send(new ObjectDestroyMessage
        {
            netId = NetId,
        });
    }
    
    public void DestroyForAll()
    {
        ObjectDestroyMessage msg = new()
        {
            netId = NetId,
        };
        foreach (var p in Player.ReadyList)
        {
            p.Connection.Send(msg);
        }
    }
    
    protected virtual void WriteOnSerialize(NetworkWriter writer)
    {
        writer.Write(ParentNetId);
    }

    protected virtual void WriteSyncVars(NetworkWriter writer)
    {
        writer.WriteVector3(LocalPosition);
        writer.WriteQuaternion(LocalRotation);
        writer.WriteVector3(Scale);
        writer.WriteByte(MovementSmoothing);
        writer.WriteBool(IsStatic);
    }

    protected virtual void WriteSyncObjects(NetworkWriter writer)
    {
    }
}