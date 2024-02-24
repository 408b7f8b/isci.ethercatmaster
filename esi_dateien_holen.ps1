# Download the zip files
Invoke-WebRequest -Uri "https://download.beckhoff.com/download/configuration-files/io/ethercat/xml-device-description/Beckhoff_EtherCAT_BKxxx_MDP.zip" -OutFile "Beckhoff_EtherCAT_BKxxx_MDP.zip"
Invoke-WebRequest -Uri "https://download.beckhoff.com/download/configuration-files/io/ethercat/xml-device-description/Beckhoff_EtherCAT_ESI_EL6070-1xxx.zip" -OutFile "Beckhoff_EtherCAT_ESI_EL6070-1xxx.zip"
Invoke-WebRequest -Uri "https://download.beckhoff.com/download/configuration-files/io/ethercat/xml-device-description/Beckhoff_EtherCAT_XML.zip" -OutFile "Beckhoff_EtherCAT_XML.zip"

# Unpack the zip files
Expand-Archive -Path "Beckhoff_EtherCAT_BKxxx_MDP.zip" -DestinationPath "ESI"
Expand-Archive -Path "Beckhoff_EtherCAT_ESI_EL6070-1xxx.zip" -DestinationPath "ESI"
Expand-Archive -Path "Beckhoff_EtherCAT_XML.zip" -DestinationPath "ESI"

Remove-Item "Beckhoff_EtherCAT_BKxxx_MDP.zip"
Remove-Item "Beckhoff_EtherCAT_ESI_EL6070-1xxx.zip"
Remove-Item "Beckhoff_EtherCAT_XML.zip"