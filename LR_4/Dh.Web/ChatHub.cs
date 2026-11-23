using Dh.Core;
using Microsoft.AspNetCore.SignalR;

namespace Dh.Web;

/// <summary>Шифротекст для одного отримувача (base64).</summary>
public sealed record Envelope(string To, string Iv, string Ct);

/// <summary>Хаб-ретранслятор: пересилає відкриті ключі Y і шифротексти. Секретів не знає.</summary>
public sealed class ChatHub(RoomRegistry rooms) : Hub
{
    public async Task Join(string room, string name, string mode, string yHex)
    {
        name = name.Trim();
        room = room.Trim();
        if (name.Length is 0 or > 32 || room.Length is 0 or > 32)
            throw new HubException("Ім'я та кімната: від 1 до 32 символів");

        var group = mode == "small" ? DhGroup.Small : DhGroup.Rfc3526;
        var y = Hex.Parse(yHex);
        if (!DhParty.IsValidY(y, group)) throw new HubException("Недопустимий відкритий ключ Y");

        var member = new Member(Context.ConnectionId, name, y, new DhParty("M", group));
        var r = rooms.Enter($"{mode}:{room}", group, member);
        await Groups.AddToGroupAsync(Context.ConnectionId, r.Key);
        await Clients.OthersInGroup(r.Key).SendAsync("System", $"{name} приєднався(-лась) до кімнати");
        await SendMembers(r);
    }

    /// <summary>Пересилання повідомлення: окремий шифротекст для кожного отримувача.</summary>
    public async Task Send(List<Envelope> items)
    {
        var r = rooms.Find(Context.ConnectionId) ?? throw new HubException("Спочатку увійдіть у кімнату");
        var from = r.Members[Context.ConnectionId];

        foreach (var e in items)
        {
            if (!r.Members.TryGetValue(e.To, out var to)) continue;
            string iv = e.Iv, ct = e.Ct;
            string? stolen = null;
            if (r.Mitm) (iv, ct, stolen) = Intercept(from, to, e);

            await Clients.Client(to.Id).SendAsync("Message", new { from = from.Id, fromName = from.Name, iv, ct });
            // журнал "що бачить сервер" для всієї кімнати
            await Clients.Group(r.Key).SendAsync("Wire",
                new { from = from.Name, to = to.Name, iv = e.Iv, ct = e.Ct, stolen });
        }
    }

    /// <summary>Увімкнути/вимкнути демонстрацію атаки "людина посередині".</summary>
    public async Task SetMitm(bool on)
    {
        var r = rooms.Find(Context.ConnectionId) ?? throw new HubException("Спочатку увійдіть у кімнату");
        r.Mitm = on;
        await Clients.Group(r.Key).SendAsync("System", on
            ? "Сервер став зловмисним: підміняє відкриті ключі Y (MITM)"
            : "Сервер знову чесно пересилає ключі");
        await SendMembers(r);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var r = rooms.Leave(Context.ConnectionId, out var m);
        if (r is not null)
        {
            await Clients.Group(r.Key).SendAsync("System", $"{m?.Name} вийшов(-ла) з кімнати");
            await SendMembers(r);
        }
        await base.OnDisconnectedAsync(exception);
    }

    /// <summary>Кожному учаснику — список інших з їхніми Y (у режимі MITM — підробленими).</summary>
    private async Task SendMembers(Room r)
    {
        foreach (var m in r.Members.Values)
        {
            var peers = r.Members.Values.Where(o => o.Id != m.Id).Select(o => new
            {
                id = o.Id,
                name = o.Name,
                y = Hex.Of(r.Mitm ? o.Fake.Y : o.Y)
            });
            await Clients.Client(m.Id).SendAsync("Members", peers.ToList(), r.Mitm);
        }
    }

    /// <summary>MITM: розшифрувати ключем (from, M) і перешифрувати ключем (M, to).</summary>
    private static (string, string, string?) Intercept(Member from, Member to, Envelope e)
    {
        try
        {
            // from шифрував під підроблений Y отримувача: K = Y_from ^ X_fake(to)
            string text = Mitm.Decrypt(to.Fake.SharedKey(from.Y), e.Iv, e.Ct);
            // to розшифровуватиме під підроблений Y відправника: K = Y_to ^ X_fake(from)
            var (iv, ct) = Mitm.Encrypt(from.Fake.SharedKey(to.Y), text);
            return (iv, ct, text);
        }
        catch (Exception)
        {
            return (e.Iv, e.Ct, null); // повідомлення зашифроване ще старими ключами
        }
    }
}
