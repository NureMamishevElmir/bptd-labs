using System.Collections.Concurrent;
using System.Globalization;
using System.Numerics;
using Dh.Core;

namespace Dh.Web;

/// <summary>Учасник кімнати. Сервер знає лише відкритий Y.</summary>
public sealed class Member(string id, string name, BigInteger y, DhParty fake)
{
    public string Id { get; } = id;
    public string Name { get; } = name;
    public BigInteger Y { get; } = y;
    public DhParty Fake { get; } = fake; // підроблена пара ключів "від імені" учасника (лише для MITM)
}

/// <summary>Кімната чату з власною групою DH.</summary>
public sealed class Room(string key, DhGroup group)
{
    public string Key { get; } = key;
    public DhGroup Group { get; } = group;
    public bool Mitm { get; set; } // режим зловмисного сервера
    public ConcurrentDictionary<string, Member> Members { get; } = new();
}

/// <summary>Реєстр кімнат і підключень.</summary>
public sealed class RoomRegistry
{
    private readonly ConcurrentDictionary<string, Room> _rooms = new();
    private readonly ConcurrentDictionary<string, Room> _byConnection = new();

    public Room Enter(string key, DhGroup group, Member m)
    {
        var room = _rooms.GetOrAdd(key, k => new Room(k, group));
        room.Members[m.Id] = m;
        _byConnection[m.Id] = room;
        return room;
    }

    public Room? Find(string connectionId) => _byConnection.GetValueOrDefault(connectionId);

    public Room? Leave(string connectionId, out Member? member)
    {
        member = null;
        if (!_byConnection.TryRemove(connectionId, out var room)) return null;
        room.Members.TryRemove(connectionId, out member);
        if (room.Members.IsEmpty) _rooms.TryRemove(room.Key, out _);
        return room;
    }
}

/// <summary>Перетворення великих чисел у hex і назад.</summary>
public static class Hex
{
    public static string Of(BigInteger n) => n.ToString("x").TrimStart('0') is { Length: > 0 } s ? s : "0";
    public static BigInteger Parse(string h) => BigInteger.Parse("0" + h, NumberStyles.HexNumber);
}
