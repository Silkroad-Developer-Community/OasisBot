using System;

namespace RSBot.Core.Objects.Item;

[Flags]
public enum ModernItemUpdateFlag : ushort
{
    RefObjID = 0x0001,
    Quantity = 0x0002,
    Durability = 0x0010,
    OptLevel = 0x0020,
    Variance = 0x0040,
    MagParams = 0x0080,
    BindingOptions = 0x0100,
    RemainTime = 0x0200,
    ItemState = 0x8000,
}
