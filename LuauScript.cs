#pragma warning disable CS8604
#pragma warning disable CS8618
#pragma warning disable IL2026

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection.Emit;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Threading.Tasks;

using static Logging;

public class LuauScript
{
    private string[] stringtable;
    private Proto[] protos;
    private int instCount = 0;
    private int optimizeLevel = 0;
    private int debugLevel = 0;

    private bool loaded = false;
    private bool internalError = false;
    private bool compilerError = false;
    private string loadError = "N/A";
    private string loadedVersion = "";

    public LuauScript(string version, string filepath, int optimizeLevel, int debugLevel)
    {
        this.optimizeLevel = optimizeLevel;
        this.debugLevel = debugLevel;
        loadedVersion = version;
        if (!File.Exists(filepath))
        {
            loadError = "Could not locate the script file to compile";
            internalError = true;
            return;
        }
        if (!File.Exists("environ\\luau-"+version+".exe"))
        {
            loadError = "Could not locate the requested Luau compiler!";
            internalError = true;
            return;
        }
        string args = "";
        if (int.Parse(version.Split(".")[1]) >= 581)
        {
            args = $"--binary -O{optimizeLevel} -g{debugLevel} {filepath}";
        } else
        {
            args = $"--compile=binary -O{optimizeLevel} -g{debugLevel} {filepath}";
        }
        string stderr = "";
        byte[] bin = Essentials.ExecProc("environ\\luau-" + version + ".exe", args, out stderr);
        if(bin.Length == 0)
        {
            compilerError = true;
            loadError = stderr;
            return;
        }
        ByteReader br = new ByteReader(bin);
        print("Constructor init, reading binary...");

        byte ver = br.ReadByte();

        if(ver < 3 || ver > 6)
        {
            loadError = "Luau version "+ver+" is not supported yet!";
            internalError = true;
            return;
        }

        byte varver = 0;

        if (ver >= 4)
            varver = br.ReadByte();

        print("Parsing Luau version ",ver);
        print("Type version ",varver);

        int strings = br.ReadVariableLen();
        stringtable = new string[strings];
        //debug($"Loading {strings} strings from the string table...");
        for (int i = 0; i < strings; i++)
        {
            string str = br.ReadRangeStr(br.ReadVariableLen());
            stringtable[i] = str;
            //debug($"String table entry #{i + 1}: {str}");
        }
        print($"Loaded {strings} strings.");

        if(varver == 3)
        {
            byte index = br.ReadByte();
            while (index != 0)
            {
                br.ReadVariableLen();
                index = br.ReadByte();
            }
        }

        int protoCount = br.ReadVariableLen();
        protos = new Proto[protoCount];
        print($"Proto count: {protoCount}");
        for (int i = 0; i < protoCount; i++)
        {
            print("Parsing proto ", i + 1, "/", protoCount);
            Proto p = ParsePR.Parse(ver, varver, ref br, ref stringtable, ref protos);
            p.bytecodeid = i;
            instCount += p.sizecode;
            for (int j = 0; j < p.sizep; ++j)
            {
                int read = br.ReadVariableLen();
                debug($"Added proto {read} to sub proto index {j}");
                p.subProtos[j] = protos[read];
            }
            // skip Logging.Debug
            br.ReadVariableLen(); br.ReadVariableLen();
            if (br.ReadByte() == 1)
            {
                warn("Line info is enabled, this is unexpected.");
                /* skip for now */
                byte linegaplog2 = br.ReadByte();
                int intervals = ((p.sizecode - 1) >> linegaplog2) + 1;
                for (int j = 0; j < p.sizecode; ++j)
                {
                    br.Skip(1);
                }
                for (int j = 0; j < intervals; ++j)
                {
                    br.Skip(4);
                }
            }
            if (br.ReadByte() == 1)
            {
                warn("Debug info is enabled, this is unexpected.");
                /* skip for now */
                int sizelocvars = br.ReadVariableLen();
                print(sizelocvars);
                for (int j = 0; j < sizelocvars; ++j)
                {
                    br.ReadVariableLen();
                    br.ReadVariableLen();
                    br.ReadVariableLen();
                    br.Skip(1);
                }
                int sizeupvalues = br.ReadVariableLen();
                for (int j = 0; j < sizeupvalues; ++j)
                {
                    br.ReadVariableLen();
                }
            }
            protos[i] = p;
        }

        loaded = true;
    }

    public string ConvertSelf()
    {
        ToHTML gen = new ToHTML();

        gen.Begin();

        /* draw header */

        gen.BeginHeader();
        gen.RawOut("# Luau Bytecode Explorer - version 1.0.2a by Lonegladiator"); gen.Break();
        gen.RawOut("# Maintained by EmK530"); gen.Break();
        if(!loaded)
        {
            gen.Break();
            gen.RawOut("<div class=\"error\">Failed to compile!</div>");
            gen.Break();
            gen.RawOut("The following error(s) were observed:");
            if (internalError)
            {
                gen.RawOut("<div class=\"error\">");
                gen.RawOut("[LBEParser] "+loadError);
                gen.EndDiv();
                return gen.GetCurrentData();
            } else if(compilerError)
            {
                gen.RawOut("<div class=\"error\">");
                if(loadError.Contains(".lua") || loadError.Contains(".tmp"))
                {
                    // hide any PC paths
                    string[] spl = loadError.Split("(");
                    string trueError = "("+spl[1];
                    string[] spl2 = spl[0].Split("\\");
                    string filename = spl2[spl2.Length-1];
                    loadError = $"{filename}{trueError}";
                    gen.RawOut(loadError);
                } else
                {
                    gen.RawOut($"[luau-{loadedVersion}] " + loadError);
                }
                gen.EndDiv();
                return gen.GetCurrentData();
            }
        }
        gen.RawOut("# Compiled with ");
        gen.RawOut($"<b>Luau {loadedVersion}</b>, <b>x64</b>.");
        gen.Break(); gen.Break();
        gen.RawOut("<u><b>Compilation Stats:</b></u>"); gen.Break();
        gen.MultiOut("# Functions: ",protos.Length); gen.Break();
        gen.MultiOut("# Instructions: ?"); gen.Break();
        gen.MultiOut("# Code Size: ", instCount); gen.Break();
        gen.MultiOut("# Optimize Level: ",optimizeLevel); gen.Break();
        gen.MultiOut("# Debug Level: ",debugLevel); gen.Break();
        if(debugLevel >= 0)
        {
            gen.Break(); gen.RawOut("# <u>Note</u>: Debug info & line info is currently ignored by the parser."); gen.Break();
        }
        gen.EndDiv();

        /* iterate through functions and output to html */
        foreach (Proto i in protos)
        {
            print("Converting function ", i.bytecodeid, "...");

            gen.RawOut("<div class=\"keyword\">function</div> ");
            gen.MultiOut("<span>anon_", i.bytecodeid, "(",Essentials.GetArguments(i),")</span>");

            /* prepare labels for all jump locations */
            Dictionary<int, int> instructionLabels = getJumpLabels(i.code);
            /* iterate through instructions and output to html */
            outputOpcodeInfo(i.code, instructionLabels, ref gen, i);

            gen.RawOut("<div class=\"keyword\">end</div>");
            gen.Break();
            gen.Break();
        }

        gen.Break();

#if DEBUG
        gen.EndDiv();
#endif

        return gen.GetCurrentData();
    }

    private void SafeLabel(int pos, ref ToHTML gen, ref Dictionary<int,int> labels)
    {
        if (labels.ContainsKey(pos))
        {
            gen.BeginLabel();
            gen.RawOut(labels[pos] + " ");
        }
        else
        {
            gen.RawOut("? ");
        }
    }

    private void outputOpcodeInfo(uint[] code, Dictionary<int, int> labels, ref ToHTML gen, Proto p)
    {
        int loop = 0;
        int lc = 0;
        for (loop = 0; loop < code.Length; loop++)
        {
            uint inst = code[loop];
            LuauOpcode opcode = (LuauOpcode)Luau.INSN_OP(inst);
            if (opcode == LuauOpcode.LOP_PREPVARARGS)
                continue;
            lc++;
            gen.RawOut("<div class=\"instruction\" line=\"-1\">");
            gen.RawOut($"<div class=\"pc\">{lc}</div>");
            gen.RawOut("<div class=\"label\">");
            if(labels.ContainsKey(loop))
            {
                gen.RawOut($"L<span class=\"number\">{labels[loop]}</span>");
            }
            gen.EndDiv();
            gen.RawOut($"<span class=\"opcode\">{opcode.ToString().Split("LOP_")[1]}</span>"); // funky
            gen.BeginOperand();
            switch (opcode)
            {
                case LuauOpcode.LOP_LOADNIL:
                    {
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_A(inst));
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_LOADB:
                    {
                        gen.BeginRegister();
                        string b = Luau.INSN_B(inst)==1 ? "true" : "false"; //uint b = Luau.INSN_B(inst);
                        gen.MultiOut(Luau.INSN_A(inst)," ",b);
                        gen.EndDiv();
                        //gen.AddComment(b == 1 ? "true" : "false", false);
                        break;
                    }
                case LuauOpcode.LOP_LOADN:
                    {
                        gen.BeginRegister();
                        gen.MultiOut(Luau.INSN_A(inst), " ", Luau.INSN_D(inst));
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_LOADK:
                    {
                        gen.BeginRegister();
                        gen.MultiOut(Luau.INSN_A(inst), " ");
                        gen.BeginConstant();
                        int d = Luau.INSN_D(inst);
                        gen.MultiOut(d, " ");
                        gen.EndDiv();
                        gen.AddComment(p.k[d]);
                        break;
                    }
                case LuauOpcode.LOP_MOVE:
                case LuauOpcode.LOP_NOT:
                case LuauOpcode.LOP_MINUS:
                case LuauOpcode.LOP_LENGTH:
                    {
                        gen.BeginRegister();
                        gen.MultiOut(Luau.INSN_A(inst), " ");
                        gen.BeginRegister();
                        gen.MultiOut(Luau.INSN_B(inst), " ");
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_GETGLOBAL:
                case LuauOpcode.LOP_SETGLOBAL:
                case LuauOpcode.LOP_LOADKX:
                    {
                        gen.BeginRegister();
                        gen.MultiOut(Luau.INSN_A(inst), " ");
                        gen.BeginConstant();
                        uint aux = code[++loop];
                        gen.MultiOut(aux, " ");
                        gen.EndDiv();
                        gen.AddComment(p.k[aux]);
                        break;
                    }
                case LuauOpcode.LOP_GETUPVAL:
                case LuauOpcode.LOP_SETUPVAL:
                    {
                        gen.BeginRegister();
                        gen.MultiOut(Luau.INSN_A(inst), " ", Luau.INSN_B(inst), " ");
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_CLOSEUPVALS:
                    {
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_A(inst));
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_GETIMPORT:
                    {
                        gen.BeginRegister();
                        gen.MultiOut(Luau.INSN_A(inst), " ");
                        uint aux = code[++loop];

                        int pathLength = (int)(aux >> 30);
                        List<string> importPathParts = new List<string>();
                        for (int a = 0; a < pathLength; a++)
                        {
                            int shiftAmount = 10 * a;
                            int index = (int)((aux >> (20 - shiftAmount)) & 1023);
                            gen.BeginConstant();
                            gen.RawOut(index + " ");
                            if (index >= p.k.Length)
                            {
                                error($"Invalid constant index for GETIMPORT: {index}.");
                            }
                            else
                            {
                                importPathParts.Add(p.k[index].ToString());
                            }
                        }
                        string importPath = string.Join('.', importPathParts);
                        gen.EndDiv();
                        gen.AddComment(importPath, false);
                        break;
                    }
                case LuauOpcode.LOP_GETTABLE:
                case LuauOpcode.LOP_SETTABLE:
                case LuauOpcode.LOP_ADD:
                case LuauOpcode.LOP_SUB:
                case LuauOpcode.LOP_MUL:
                case LuauOpcode.LOP_DIV:
                case LuauOpcode.LOP_MOD:
                case LuauOpcode.LOP_POW:
                case LuauOpcode.LOP_AND:
                case LuauOpcode.LOP_OR:
                case LuauOpcode.LOP_CONCAT:
                    {
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_A(inst)+" ");
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_B(inst) + " ");
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_C(inst) + " ");
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_GETTABLEKS:
                case LuauOpcode.LOP_SETTABLEKS:
                case LuauOpcode.LOP_NAMECALL:
                    {
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_A(inst) + " ");
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_B(inst) + " ");
                        uint aux = code[++loop];
                        gen.BeginConstant();
                        gen.RawOut(aux + " ");
                        gen.EndDiv();
                        gen.AddComment(p.k[aux]);
                        break;
                    }
                case LuauOpcode.LOP_GETTABLEN:
                case LuauOpcode.LOP_SETTABLEN:
                    {
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_A(inst) + " ");
                        gen.BeginRegister();
                        gen.RawOut($"{Luau.INSN_B(inst)} {Luau.INSN_C(inst)+1} ");
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_NEWCLOSURE:
                    {
                        gen.BeginRegister();
                        int d = Luau.INSN_D(inst);
                        gen.RawOut(Luau.INSN_A(inst) + $" P{d} ");
                        gen.EndDiv();
                        gen.AddComment("anon_" + d);
                        break;
                    }
                case LuauOpcode.LOP_CALL:
                    {
                        gen.BeginRegister();
                        gen.RawOut($"{Luau.INSN_A(inst)} {((int)Luau.INSN_B(inst))-1} {((int)Luau.INSN_C(inst))-1}");
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_RETURN:
                    {
                        gen.BeginRegister();
                        gen.RawOut($"{Luau.INSN_A(inst)} {((int)Luau.INSN_B(inst)) - 1}");
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_JUMP:
                case LuauOpcode.LOP_JUMPBACK:
                    {
                        SafeLabel(loop + Luau.INSN_D(inst) + 1, ref gen, ref labels);
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_JUMPIF:
                case LuauOpcode.LOP_JUMPIFNOT:
                case LuauOpcode.LOP_FORNPREP:
                case LuauOpcode.LOP_FORNLOOP:
                case LuauOpcode.LOP_FORGPREP:
                    {
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_A(inst) + " ");
                        SafeLabel(loop + Luau.INSN_D(inst) + 1, ref gen, ref labels);
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_JUMPIFEQ:
                case LuauOpcode.LOP_JUMPIFLE:
                case LuauOpcode.LOP_JUMPIFLT:
                case LuauOpcode.LOP_JUMPIFNOTEQ:
                case LuauOpcode.LOP_JUMPIFNOTLE:
                case LuauOpcode.LOP_JUMPIFNOTLT:
                    {
                        uint aux = code[++loop];
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_A(inst) + " ");
                        gen.BeginRegister();
                        gen.RawOut(aux + " ");
                        SafeLabel(loop + Luau.INSN_D(inst), ref gen, ref labels);
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_ADDK:
                case LuauOpcode.LOP_SUBK:
                case LuauOpcode.LOP_MULK:
                case LuauOpcode.LOP_DIVK:
                case LuauOpcode.LOP_MODK:
                case LuauOpcode.LOP_POWK:
                case LuauOpcode.LOP_ANDK:
                case LuauOpcode.LOP_ORK:
                    {
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_A(inst) + " ");
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_B(inst) + " ");
                        gen.BeginConstant();
                        uint c = Luau.INSN_C(inst);
                        gen.RawOut(c + " ");
                        gen.EndDiv();
                        gen.AddComment(p.k[c]);
                        break;
                    }
                case LuauOpcode.LOP_NEWTABLE:
                    {
                        gen.BeginRegister();
                        uint aux = code[++loop];
                        gen.RawOut($"{Luau.INSN_A(inst)} {Luau.INSN_B(inst)} {aux} ");
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_DUPTABLE:
                    {
                        gen.BeginRegister();
                        gen.MultiOut(Luau.INSN_A(inst), " ");
                        gen.BeginConstant();
                        int d = Luau.INSN_D(inst);
                        gen.MultiOut(d, " ");
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_DUPCLOSURE:
                    {
                        gen.BeginRegister();
                        gen.MultiOut(Luau.INSN_A(inst), " ");
                        gen.BeginConstant();
                        int d = Luau.INSN_D(inst);
                        gen.MultiOut(d, " ");
                        gen.EndDiv();
                        object o = p.k[d];
                        if (o != null && o.GetType() == typeof(Closure))
                        {
                            gen.AddComment("anon_"+((Closure)o).sourceID, false);
                        } else
                        {
                            gen.AddComment("???", false);
                        }
                        break;
                    }
                case LuauOpcode.LOP_SETLIST:
                    {
                        gen.BeginRegister();
                        gen.MultiOut(Luau.INSN_A(inst), " ");
                        gen.BeginRegister();
                        gen.MultiOut(Luau.INSN_B(inst), " ", ((int)Luau.INSN_C(inst)) - 1, " ");
                        uint aux = code[++loop];
                        gen.MultiOut(aux, " ");
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_FORGLOOP:
                    {
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_A(inst) + " ");
                        SafeLabel(loop + Luau.INSN_D(inst) + 1, ref gen, ref labels);
                        uint aux = code[++loop];
                        gen.RawOut(aux + " ");
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_FORGPREP_NEXT:
                case LuauOpcode.LOP_FORGPREP_INEXT:
                    {
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_A(inst) + " ");
                        SafeLabel(loop + Luau.INSN_D(inst) + 1, ref gen, ref labels);
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_FASTCALL3:
                    {
                        gen.RawOut(Luau.INSN_A(inst) + " ");
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_B(inst) + " ");
                        SafeLabel(loop + (int)Luau.INSN_C(inst) + 1, ref gen, ref labels);
                        uint aux = code[++loop];
                        gen.RawOut($"{aux & 0xFF} {(aux >> 8) & 0xFF} ");
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_GETVARARGS:
                    {
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_A(inst) + " ");
                        gen.RawOut((((int)Luau.INSN_B(inst))-1) + " ");
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_JUMPX:
                    {
                        SafeLabel(loop + Luau.INSN_E(inst) + 1, ref gen, ref labels);
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_FASTCALL:
                    {
                        gen.RawOut(Luau.INSN_A(inst) + " ");
                        SafeLabel(loop + (int)Luau.INSN_C(inst) + 2, ref gen, ref labels);
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_COVERAGE:
                    {
                        gen.RawOut(Luau.INSN_E(inst) + " ");
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_CAPTURE:
                    {
                        string name = ((LuauCaptureType)Luau.INSN_A(inst)).ToString().Split("_")[1];
                        gen.RawOut(name + " ");
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_B(inst) + " ");
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_SUBRK:
                case LuauOpcode.LOP_DIVRK:
                    {
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_A(inst) + " ");
                        gen.BeginConstant();
                        uint b = Luau.INSN_B(inst);
                        gen.RawOut(b + " ");
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_C(inst) + " ");
                        gen.EndDiv();
                        gen.AddComment(p.k[b]);
                        break;
                    }
                case LuauOpcode.LOP_FASTCALL1:
                    {
                        gen.RawOut(Luau.INSN_A(inst) + " ");
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_B(inst) + " ");
                        SafeLabel(loop + (int)Luau.INSN_C(inst) + 2, ref gen, ref labels);
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_FASTCALL2:
                    {
                        gen.RawOut(Luau.INSN_A(inst) + " ");
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_B(inst) + " ");
                        SafeLabel(loop + (int)Luau.INSN_C(inst) + 1, ref gen, ref labels);
                        uint aux = code[++loop];
                        gen.BeginRegister();
                        gen.RawOut(aux + " ");
                        gen.EndDiv();
                        break;
                    }
                case LuauOpcode.LOP_FASTCALL2K:
                    {
                        gen.RawOut(Luau.INSN_A(inst) + " ");
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_B(inst) + " ");
                        SafeLabel(loop + (int)Luau.INSN_C(inst) + 1, ref gen, ref labels);
                        uint aux = code[++loop];
                        gen.BeginConstant();
                        gen.RawOut(aux + " ");
                        gen.EndDiv();
                        gen.AddComment(p.k[aux]);
                        break;
                    }
                case LuauOpcode.LOP_JUMPXEQKNIL:
                case LuauOpcode.LOP_JUMPXEQKB:
                case LuauOpcode.LOP_JUMPXEQKN:
                case LuauOpcode.LOP_JUMPXEQKS:
                    {
                        uint AUX = code[++loop];
                        gen.BeginRegister();
                        gen.RawOut(Luau.INSN_A(inst) + " ");
                        gen.BeginConstant();
                        uint ki = (AUX & 16777215);
                        gen.RawOut(ki + " ");
                        SafeLabel(loop + Luau.INSN_D(inst), ref gen, ref labels);
                        bool flip = (AUX >> 31) == 1 ? false : true;
                        if(!flip)
                            gen.RawOut("NOT ");
                        gen.EndDiv();
                        gen.AddComment(p.k[ki]);
                        break;
                    }
                default:
                    {
                        //warn("Unsupported: ", opcode);
                        gen.EndDiv();
                        break;
                    }
            }
            gen.EndDiv();
        }
    }

    private Dictionary<int,int> getJumpLabels(uint[] code)
    {
        int loop = 0;
        List<int> labels = new List<int>();
        for (loop = 0; loop < code.Length; loop++)
        {
            uint inst = code[loop];
            LuauOpcode opcode = (LuauOpcode)Luau.INSN_OP(inst);
            switch (opcode)
            {
                case LuauOpcode.LOP_JUMP:
                case LuauOpcode.LOP_JUMPBACK:
                case LuauOpcode.LOP_JUMPIF:
                case LuauOpcode.LOP_JUMPIFNOT:
                    {
                        int target = loop + Luau.INSN_D(inst) + 1;
                        //debug("Found a jump on ", loop, " - ", target);
                        if (!labels.Contains(target)) { labels.Add(target); }
                        break;
                    }
                case LuauOpcode.LOP_JUMPIFEQ:
                case LuauOpcode.LOP_JUMPIFLE:
                case LuauOpcode.LOP_JUMPIFLT:
                case LuauOpcode.LOP_JUMPIFNOTEQ:
                case LuauOpcode.LOP_JUMPIFNOTLE:
                case LuauOpcode.LOP_JUMPIFNOTLT:
                    {
                        loop++;
                        int target = loop + Luau.INSN_D(inst);
                        //debug("Found a conditional jump on ", loop, " - ", target);
                        if (!labels.Contains(target)) { labels.Add(target); }
                        break;
                    }
                case LuauOpcode.LOP_JUMPXEQKNIL:
                case LuauOpcode.LOP_JUMPXEQKB:
                case LuauOpcode.LOP_JUMPXEQKN:
                case LuauOpcode.LOP_JUMPXEQKS:
                    {
                        uint AUX = code[++loop];
                        bool flip = (AUX >> 31) == 1 ? false : true;
                        int target = loop + Luau.INSN_D(inst);
                        //debug("Found a special conditional jump on ", loop, " - ", target);
                        if (!labels.Contains(target)) { labels.Add(target); }
                        break;
                    }
                case LuauOpcode.LOP_FASTCALL:
                case LuauOpcode.LOP_FASTCALL1:
                    {
                        int target = (int)(loop + Luau.INSN_C(inst) + 2);
                        //debug("Found a fastcall jump on ", loop, " - ", target);
                        if (!labels.Contains(target)) { labels.Add(target); }
                        break;
                    }
                case LuauOpcode.LOP_FORGPREP:
                case LuauOpcode.LOP_FORGPREP_NEXT:
                case LuauOpcode.LOP_FORGPREP_INEXT:
                    {
                        int target = (int)(loop + Luau.INSN_D(inst) + 1);
                        //debug("Found a for loop jump on ", loop, " - ", target);
                        if (!labels.Contains(target)) { labels.Add(target); }
                        break;
                    }
                case LuauOpcode.LOP_FORGLOOP:
                    {
                        loop++;
                        int target = (int)(loop + Luau.INSN_D(inst));
                        //debug("Found a for loop jump on ", loop, " - ", target);
                        if (!labels.Contains(target)) { labels.Add(target); }
                        break;
                    }
                case LuauOpcode.LOP_GETGLOBAL:
                case LuauOpcode.LOP_SETGLOBAL:
                case LuauOpcode.LOP_GETIMPORT:
                case LuauOpcode.LOP_GETTABLEKS:
                case LuauOpcode.LOP_SETTABLEKS:
                case LuauOpcode.LOP_NAMECALL:
                case LuauOpcode.LOP_NEWTABLE:
                case LuauOpcode.LOP_SETLIST:
                case LuauOpcode.LOP_LOADKX:
                case LuauOpcode.LOP_FASTCALL2:
                case LuauOpcode.LOP_FASTCALL2K:
                    {
                        //print("AUX skip: ", opcode);
                        loop++;
                        continue;
                    }
                default:
                    {
                        //warn("Unsupported: ", opcode);
                        break;
                    }
            }
        }
        labels.Sort();
        Dictionary<int, int> ret = new Dictionary<int, int>();
        int num = 0;
        foreach(int i in labels)
        {
            if(ret.TryAdd(i, num))
            {
                num++;
            }
        }
        return ret;
    }
}