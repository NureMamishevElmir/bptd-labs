# ЛР-3. Програмна реалізація хеш-функцій

Безпека програм та даних · Мамішев Е. І., ПЗПІ-23-6, варіант 13 · .NET 8

## Структура

| Проєкт | Призначення |
|---|---|
| `Hash.Core` | хеш-функція `Hash13`, лавинний ефект `Avalanche`, пошук колізій `CollisionFinder` (вставка nonce у docx/png/код/інше), генерація зразків |
| `Hash.Web` | веб-інтерфейс на http://localhost:5103 (дайджест, лавинний ефект, колізія) |
| `Hash.Cli` | консольна програма |
| `Hash.Tests` | тести xUnit |
| `samples/` | документи: оригінал, `_edit` (зміна без nonce), `_changed` (колізія, 8 біт), `_changed_4bit`, `_changed_2bit`; `results.txt` — таблиця |

## Запуск

```
dotnet test
dotnet run --project Hash.Web                    # http://localhost:5103
dotnet run --project Hash.Cli -- digest samples/nakaz.docx 8
dotnet run --project Hash.Cli -- collide samples/picture.png samples/picture_edit.png out.png 8
dotnet run --project Hash.Cli -- avalanche
dotnet run --project Hash.Cli -- make-samples samples
```

## Хеш-функція

Стан 64 біти, початкове значення `0x1313C0DE20260013`. Для кожного байта:
`h ^= b; h *= 0x9E3779B97F4A7C15; h = rotl(h, 27)`. Після тексту підмішується довжина,
далі — фінальний перемішувач xorshift-multiply. 64-бітний результат XOR-згортається
64→32→16→8→4→2 до потрібної розрядності (2, 4 або 8 біт).

## Колізії

Nonce (шістнадцятковий номер спроби) вставляється у місце, що не псує формат:
docx — коментар `<!-- nonce: … -->` у `word/document.xml` (zip перезбирається);
png — чанк `tEXt` з коректним CRC перед `IEND`; код — коментар у кінці; інші файли — байти в кінці.
Середня кількість спроб ≈ 2^біт.
