#pragma warning disable CS8601
#pragma warning disable CS8604
#pragma warning disable CS8618
#pragma warning disable CS8625

/* PARSES INDIVIDUAL PROTOS FROM LUAU BYTECODE */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

using static Logging;

public class Proto
{
    public byte nups;
    public byte numparams;
    public byte is_vararg;
    public byte maxstacksize;
    public byte flags = 0;
    public int bytecodeid = 0;
    public int sizecode;
    public int sizek;
    public int sizep = 0;
    public uint[] code;
    public uint[] codeentry;
    public object[] k;
    public Proto[] subProtos;
}

public class Closure
{
    public int sourceID;
}

public static class ParsePR
{
    public static Proto Parse(byte v, byte tv, ref ByteReader br, ref string[] tbl, ref Proto[] subs)
    {
        Proto p = new Proto();
        p.maxstacksize = br.ReadByte();
        p.numparams = br.ReadByte();
        p.nups = br.ReadByte();
        p.is_vararg = br.ReadByte();
        if (v >= 4)
        {
            p.flags = br.ReadByte();
            if (tv >= 1 && tv <= 3)
            {
                br.Skip(br.ReadVariableLen()); // not supported for now
            }
        }
        p.sizecode = br.ReadVariableLen();
        p.code = new uint[p.sizecode];
        for (int j = 0; j < p.sizecode; j++)
        {
            p.code[j] = br.ReadUInt32(Endian.Little);
        }
        debug("Loaded ", p.sizecode, " instructions.");
        p.sizek = br.ReadVariableLen();
        p.k = new object[p.sizek];
        int tables = 0;
        for (int j = 0; j < p.sizek; ++j)
        {
            LuauBytecodeTag read = (LuauBytecodeTag)br.ReadByte();
            switch (read)
            {
                case LuauBytecodeTag.LBC_CONSTANT_NIL:
                    p.k[j] = null;
                    break;
                case LuauBytecodeTag.LBC_CONSTANT_BOOLEAN:
                    p.k[j] = br.ReadByte() == 1;
                    break;
                case LuauBytecodeTag.LBC_CONSTANT_NUMBER:
                    p.k[j] = BitConverter.ToDouble(br.ReadRange(8));
                    break;
                case LuauBytecodeTag.LBC_CONSTANT_VECTOR:
                    warn("Found unimplemented vector constant.");
                    br.Skip(16);
                    break;
                case LuauBytecodeTag.LBC_CONSTANT_STRING:
                    int id = br.ReadVariableLen();
                    string? rd = id == 0 ? null : tbl[id - 1];
                    if (rd == null)
                        warn("Invalid string constant ID: ",id);
                    p.k[j] = rd;
                    break;
                case LuauBytecodeTag.LBC_CONSTANT_CLOSURE:
                    int fid = br.ReadVariableLen();
                    Closure cl = new Closure();
                    cl.sourceID = fid;
                    p.k[j] = cl;
                    break;
                case LuauBytecodeTag.LBC_CONSTANT_TABLE:
                    Dictionary<string, object> kt = new Dictionary<string, object>(); // key table
                    int keys = br.ReadVariableLen();
                    for (int k = 0; k < keys; ++k)
                    {
                        int temp = br.ReadVariableLen();
                        kt.Add(tbl[temp - tables], null); // hacky solution because there was a bug
                    }
                    tables++;
                    p.k[j] = kt;
                    break;
                case LuauBytecodeTag.LBC_CONSTANT_IMPORT:
                    uint aux = br.ReadUInt32(Endian.Little);
                    int pathLength = (int)(aux >> 30);
                    List<string> importPathParts = new List<string>();
                    for (int a = 0; a < pathLength; a++)
                    {
                        int shiftAmount = 10 * a;
                        int index = (int)((aux >> (20 - shiftAmount)) & 1023);
                        if (index >= p.k.Length)
                        {
                            error($"Invalid constant index {index}.");
                        }
                        else
                        {
                            importPathParts.Add(p.k[index].ToString());
                        }
                    }
                    string importPath = string.Join('.', importPathParts);
                    //debug("Found import path: ", importPath);
                    break;
                default:
                    error("Invalid constant type found: ",read);
                    break;
            }
        }
        debug("Loaded ", p.sizek, " constants.");
        p.sizep = br.ReadVariableLen();
        p.subProtos = new Proto[p.sizep];
        return p;
    }
}