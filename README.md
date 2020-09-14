# Xiropht-Solo-Miner
<h2>Official Xiropht Solo Miner.</h2>


**Note: This Solo Miner is the standard miner tool of Xiropht, this is just a compatible example tool with the actual mining system who permit to other developers to improve it or to create their own miner tool.**

**Current math generator system can be improved easily.**

**External developers have the freedom to edit this solo mining tool for increase his performance**

<h2>Windows:</h2>

-> This miner require Netframework 4.6.1 minimum and the Xiropht Connector All library: https://github.com/XIROPHT/Xiropht-Connector-All

<h2>Linux:</h2>

-> This miner require Mono for be executed or compiled into a binary file follow those instructions:

~~~text
- apt-get install mono-complete

- mono Xiropht-Solo-Miner.exe
~~~

-> You can also make your own linux binary for don't have to install mono:

~~~text
- mkbundle Xiropht-Solo-Miner.exe -o Xiropht-Solo-Miner Xiropht-Connector-All.dll Newtonsoft.Json.dll  --deps -z --static

- ./Xiropht-Solo-Miner
~~~

**A batch script has been provided to show a better example here: https://github.com/XIROPHT/Xiropht-Solo-Miner/tree/master/MonoScript**

The config.ini file of the miner is initialized after the first running.

This release as been compiled into Debug Mode, because we are currently in private test.

Hashrate test [Updated 14/09/2020]:

-> Raspberry PI 3 (On Raspberian OS): 9,2 KH/s.

-> Ryzen 7 2700x No OC (On Windows 10): 390 to 400 KH/s.


<h3>Help:</h3>

For more informations about how work the Xiropht Mining System, please check out the Wiki: https://github.com/XIROPHT/Xiropht-Solo-Miner/wiki


**Newtonsoft.Json library is used since version 0.7.3.3R:** https://github.com/JamesNK/Newtonsoft.Json

**Developers:**

- Xiropht (Sam Segura)
