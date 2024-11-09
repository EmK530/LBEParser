# LBEParser
A CLI that I created to make [my restoration](https://luau.emk530.net) of lonegladiator's [Luau Bytecode Explorer](https://luau.lonegladiator.dev) as it has fallen behind in versions.<br>
This program will compile an input Luau script into binary, parse it and convert into HTML data compatible with LBE.

## Support
LBEParser currently supports from Luau version 0.539 to the latest version.

Why? Versions before 0.539 use a Luau version below 3 which does not seem to parse properly,<br>
which is probably why the Luau VM has officially dropped support for it.

## How to use
The program expects to only receive 4 input arguments:<br>
`LBEParser.exe [version] [script-path] [O-level] [g-level]`

Usage example:<br>
`LBEParser.exe 0.632 input.lua 2 0`<br>
This compiles `input.lua` with O2 and g0, using Luau 0.632.

## Requirements
For LBEParser to properly compile any code, it needs to have access to all the different Luau compilers.<br>
To properly provide this, it expects an `environ` folder to exist in its working directory.<br>
Then to properly add a Luau version, it needs to have this name format: `luau-{version}.exe`<br>
An example of a proper version name is: `luau-0.632.exe`

Downloading all of the Luau compilers may be time consuming though,<br>
so you can get versions 0.539 to 0.651 [here](https://drive.google.com/file/d/1AXNJW94KSla-wcHS1BDQEFgzyJCE_p4Z/view).
