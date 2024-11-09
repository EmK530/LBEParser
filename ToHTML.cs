using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static System.Net.Mime.MediaTypeNames;

public class ToHTML
{
    string currentOutput = "";

    public void Begin() {
#if DEBUG
        currentOutput = "<div id=\"bytecode\" class=\"bg-black overflow-y-scroll font-mono text-sm h-[92vh]\">";
#endif
    }
    public void BeginHeader() { currentOutput += "<div class=\"header\">"; }
    public void BeginOperand() { currentOutput += "<div class=\"operand\">"; }
    public void BeginRegister() { currentOutput += "<div class=\"operand-register\">R</div>"; }
    public void BeginConstant() { currentOutput += "<div class=\"operand-constant\">K</div>"; }
    public void BeginLabel() { currentOutput += "<div class=\"operand-label\">L</div>"; }
    public void AddComment(object any, bool showAsString = true) {
        string thing = any.GetType() == typeof(string) ? "'" : "";
        currentOutput += $"<div class=\"comment\">; {(showAsString?thing:"")+any.ToString()+(showAsString?thing:"")}</div>";
    }
    public void Break() { currentOutput += "<br>"; }
    public void EndDiv() { currentOutput += "</div>"; }
    public string GetCurrentData() { return currentOutput; }
    public void RawOut(object txt) { currentOutput += txt.ToString(); }
    public void MultiOut(params object[] vars) {
        foreach(object i in vars) {
            currentOutput += i.ToString();
        }
    }
}