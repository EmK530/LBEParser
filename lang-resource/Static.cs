public static class Luau
{
    public static uint INSN_OP(uint insn) => insn & 0xFF;
    public static uint INSN_A(uint insn) => (insn >> 8) & 0xFF;
    public static uint INSN_B(uint insn) => (insn >> 16) & 0xFF;
    public static uint INSN_C(uint insn) => (insn >> 24) & 0xFF;
    public static int INSN_D(uint insn) => (int)((int)insn >> 16);
    public static int INSN_E(uint insn) => (int)(insn >> 8);
}

enum LuauBytecodeTag
{
    LBC_CONSTANT_NIL = 0,
    LBC_CONSTANT_BOOLEAN,
    LBC_CONSTANT_NUMBER,
    LBC_CONSTANT_STRING,
    LBC_CONSTANT_IMPORT,
    LBC_CONSTANT_TABLE,
    LBC_CONSTANT_CLOSURE,
    LBC_CONSTANT_VECTOR,
};

enum LuauBytecodeType
{
    LBC_TYPE_NIL = 0,
    LBC_TYPE_BOOLEAN,
    LBC_TYPE_NUMBER,
    LBC_TYPE_STRING,
    LBC_TYPE_TABLE,
    LBC_TYPE_FUNCTION,
    LBC_TYPE_THREAD,
    LBC_TYPE_USERDATA,
    LBC_TYPE_VECTOR,
    LBC_TYPE_BUFFER,

    LBC_TYPE_ANY = 15,

    LBC_TYPE_TAGGED_USERDATA_BASE = 64,
    LBC_TYPE_TAGGED_USERDATA_END = 64 + 32,

    LBC_TYPE_OPTIONAL_BIT = 1 << 7,

    LBC_TYPE_INVALID = 256,
};

enum LuauCaptureType
{
    LCT_VAL = 0,
    LCT_REF,
    LCT_UPVAL,
};