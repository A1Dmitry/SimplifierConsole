namespace RicisCore;

public enum EntityState
{
    Finite, // Обычное значение F
    TypedZero, // 0_F (Архив)
    IndexedInfinity, // ∞_F
    Monolith // Кортеж (F, G)
}