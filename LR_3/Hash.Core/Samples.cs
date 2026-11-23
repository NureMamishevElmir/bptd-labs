using System.Text;

namespace Hash.Core;

/// <summary>Три зразкові документи (оригінал і змінена версія без nonce).</summary>
public static class Samples
{
    // ---------- Word ----------
    static byte[] Nakaz(string deadline) => Docx.Create(
        "НАКАЗ № 13",
        new[]
        {
            "Харківський національний університет радіоелектроніки",
            "Кафедра програмної інженерії, 23 листопада 2026 р.",
            "Про виконання лабораторної роботи № 3 «Програмна реалізація хеш-функцій».",
            $"1. Студенту групи ПЗПІ-23-6 Мамішеву Е. І. виконати лабораторну роботу до {deadline}.",
            "2. Звіт подати в електронному вигляді разом з вихідним кодом.",
            "3. Контроль за виконанням наказу залишаю за собою.",
        });

    public static byte[] NakazOriginal() => Nakaz("15 грудня 2026 р.");
    public static byte[] NakazEdited() => Nakaz("22 грудня 2026 р."); // змінено дату

    // ---------- Вихідний код ----------
    const string Program = """
        # Розрахунок стипендії (зразок вихідного коду для ЛР-3)
        RATE = 0.20  # надбавка за відмінне навчання


        def scholarship(base: float, excellent: bool) -> float:
            if excellent:
                return round(base * (1 + RATE), 2)
            return base


        if __name__ == "__main__":
            print("Стипендія:", scholarship(2000.0, True))

        """;

    public static byte[] CodeOriginal() => Encoding.UTF8.GetBytes(Program);
    public static byte[] CodeEdited() => Encoding.UTF8.GetBytes(Program.Replace("RATE = 0.20", "RATE = 0.25")); // змінено рядок

    // ---------- Зображення ----------
    static byte[] Picture((byte, byte, byte) sun) => Png.Encode(240, 160, (x, y) =>
    {
        if ((x - 190) * (x - 190) + (y - 40) * (y - 40) <= 22 * 22) return sun; // сонце
        if (x >= 50 && x < 130 && y >= 80 && y < 140) return (180, 60, 40);       // будинок
        if (y >= 40 && y < 80 && Math.Abs(x - 90) <= y - 40) return (120, 40, 30); // дах
        if (y >= 140) return (60, 160, 60);                                        // трава
        return (150, 200, 255);                                                    // небо
    });

    public static byte[] PictureOriginal() => Picture((255, 220, 0)); // жовте сонце
    public static byte[] PictureEdited() => Picture((255, 80, 0));    // помаранчеве сонце
}
