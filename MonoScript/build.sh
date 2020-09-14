mkbundle --static --cross mono-5.20.1-ubuntu-16.04-x64  Xiropht-Solo-Miner.exe -o Xiropht-Solo-Miner-Ubuntu-x64 --deps Xiropht-Connector-All.dll Newtonsoft.Json.dll --options -O=all,-deadce,-inline --config config.config --machine-config machine.config --options -O=all,-deadce,-inline

mkbundle --static --cross mono-5.20.1-raspbian-9-arm  Xiropht-Solo-Miner.exe -o Xiropht-Solo-Miner-Raspberry --deps Xiropht-Connector-All.dll Newtonsoft.Json.dll --options -O=all,-deadce,-inline --config config.config --machine-config machine.config --options -O=all,-deadce,-inline

mkbundle --static --cross mono-5.20.1-debian-9-x64  Xiropht-Solo-Miner.exe -o Xiropht-Solo-Miner-Debian9-x64 --deps Xiropht-Connector-All.dll Newtonsoft.Json.dll --options -O=all,-deadce,-inline --config config.config --machine-config machine.config --options -O=all,-deadce,-inline
