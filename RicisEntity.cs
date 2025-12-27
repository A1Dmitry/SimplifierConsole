namespace RicisCore;

// --- 3. ЯДРО (Recursive Identity Entity) ---
public class RicisEntity
{
    // Конструктор значения
    public RicisEntity(double value, RicisType type, string symbol = null)
    {
        Magnitude = value;
        Type = type;
        IndexSymbol = symbol ?? value.ToString();

        // A3: Typed Zero detection
        // Используем Epsilon для float сравнений
        State = Math.Abs(value) < 1e-10 ? EntityState.TypedZero : EntityState.Finite;
    }

    // Приватный конструктор для внутренних состояний
    private RicisEntity(EntityState state, RicisType type, double mag, string symbol)
    {
        State = state;
        Type = type;
        Magnitude = mag;
        IndexSymbol = symbol;
        Components = new List<RicisEntity>();
    }

    public double Magnitude { get; } // Значение (для Finite)
    public RicisType Type { get; } // Категория T(X)
    public EntityState State { get; }

    // Для символьных вычислений (хранит "имя" переменной или формулу индекса)
    public string IndexSymbol { get; }

    // Хранилище для Монолитов
    public List<RicisEntity> Components { get; }

    // Фабрика Монолита
    public static RicisEntity CreateMonolith(RicisEntity a, RicisEntity b)
    {
        var m = new RicisEntity(EntityState.Monolith, RicisType.CreateTuple(a.Type, b.Type), 0,
            $"({a.IndexSymbol}, {b.IndexSymbol})");
        m.Components.Add(a);
        m.Components.Add(b);
        return m;
    }

    // --- ОПЕРАТОРЫ RICIS ---

    public static RicisEntity operator +(RicisEntity a, RicisEntity b)
    {
        // Protocol: Incompatible Types -> Monolith
        if (!a.Type.IsCompatibleWith(b.Type))
            return CreateMonolith(a, b);

        // Logic: Infinity + Finite = Infinity (Index preserved)
        if (a.State == EntityState.IndexedInfinity) return a;
        if (b.State == EntityState.IndexedInfinity) return b;

        return new RicisEntity(a.Magnitude + b.Magnitude, a.Type);
    }

    public static RicisEntity operator -(RicisEntity a, RicisEntity b)
    {
        if (!a.Type.IsCompatibleWith(b.Type))
            throw new InvalidOperationException(
                "Cannot subtract incompatible types (Monolith subtraction undefined in v3.2)");

        // A7: ∞_F - ∞_G -> ∞_{F-G} (Requires symbolic algebra, simplified here)
        if (a.State == EntityState.IndexedInfinity && b.State == EntityState.IndexedInfinity)
            return new RicisEntity(EntityState.IndexedInfinity, a.Type, double.PositiveInfinity,
                $"{a.IndexSymbol}-{b.IndexSymbol}");

        return new RicisEntity(a.Magnitude - b.Magnitude, a.Type);
    }

    public static RicisEntity operator *(RicisEntity a, RicisEntity b)
    {
        var newType = RicisType.Operate(a.Type, b.Type, "*");
        var newSymbol = $"{a.IndexSymbol}*{b.IndexSymbol}";

        // A6: Identity Recovery (0_F * ∞_F -> F)
        if ((a.State == EntityState.TypedZero && b.State == EntityState.IndexedInfinity) ||
            (a.State == EntityState.IndexedInfinity && b.State == EntityState.TypedZero))
            // Если типы совместимы, происходит коллапс в конечное значение
            // В полной версии здесь нужно символьное сокращение (x * 1/x)
            return new RicisEntity(1.0, newType, "Recovered_Identity");

        // A10: F * 0 -> 0_F (Сохранение типа)
        if (a.State == EntityState.TypedZero || b.State == EntityState.TypedZero)
            return new RicisEntity(EntityState.TypedZero, newType, 0, newSymbol);

        if (a.State == EntityState.IndexedInfinity || b.State == EntityState.IndexedInfinity)
            return new RicisEntity(EntityState.IndexedInfinity, newType, double.PositiveInfinity, newSymbol);

        return new RicisEntity(a.Magnitude * b.Magnitude, newType, newSymbol);
    }

    public static RicisEntity operator /(RicisEntity a, RicisEntity b)
    {
        var newType = RicisType.Operate(a.Type, b.Type, "/");
        var newSymbol = $"{a.IndexSymbol}/{b.IndexSymbol}";

        // A4: 0_F / 0_G -> ∞_{F/G} (Сингулярность рождает структуру)
        if (a.State == EntityState.TypedZero && b.State == EntityState.TypedZero)
            return new RicisEntity(EntityState.IndexedInfinity, newType, double.PositiveInfinity, newSymbol);

        // Деление на ноль (Классическое) -> RICIS Infinity
        if (b.State == EntityState.TypedZero)
            return new RicisEntity(EntityState.IndexedInfinity, newType, double.PositiveInfinity, newSymbol);

        // A5: ∞ / ∞ -> Ratio of Indices
        if (a.State == EntityState.IndexedInfinity && b.State == EntityState.IndexedInfinity)
            return new RicisEntity(1.0, newType, "Index_Ratio"); // Упрощение

        return new RicisEntity(a.Magnitude / b.Magnitude, newType, newSymbol);
    }

    public override string ToString()
    {
        switch (State)
        {
            case EntityState.TypedZero: return $"0_[{Type}:{IndexSymbol}]";
            case EntityState.IndexedInfinity: return $"∞_[{Type}:{IndexSymbol}]";
            case EntityState.Monolith: return $"Monolith({string.Join(", ", Components.Select(c => c.ToString()))})";
            default: return $"{Magnitude} ({Type})";
        }
    }
}

// --- 4. ИНТЕРПРЕТАТОР ВЫРАЖЕНИЙ (Expression Visitor) ---