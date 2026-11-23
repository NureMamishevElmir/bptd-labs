# Розрахунок стипендії (зразок вихідного коду для ЛР-3)
RATE = 0.25  # надбавка за відмінне навчання


def scholarship(base: float, excellent: bool) -> float:
    if excellent:
        return round(base * (1 + RATE), 2)
    return base


if __name__ == "__main__":
    print("Стипендія:", scholarship(2000.0, True))
# nonce: 41
