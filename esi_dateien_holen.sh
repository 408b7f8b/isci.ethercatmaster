#!/bin/bash

# Download the zip files
wget https://download.beckhoff.com/download/configuration-files/io/ethercat/xml-device-description/Beckhoff_EtherCAT_BKxxx_MDP.zip -O Beckhoff_EtherCAT_BKxxx_MDP.zip
wget https://download.beckhoff.com/download/configuration-files/io/ethercat/xml-device-description/Beckhoff_EtherCAT_ESI_EL6070-1xxx.zip -O Beckhoff_EtherCAT_ESI_EL6070-1xxx.zip
wget https://download.beckhoff.com/download/configuration-files/io/ethercat/xml-device-description/Beckhoff_EtherCAT_XML.zip -O Beckhoff_EtherCAT_XML.zip

# Unpack the zip files
unzip Beckhoff_EtherCAT_BKxxx_MDP.zip -d ESI
unzip Beckhoff_EtherCAT_ESI_EL6070-1xxx.zip -d ESI
unzip Beckhoff_EtherCAT_XML.zip -d ESI

rm Beckhoff_EtherCAT_BKxxx_MDP.zip
rm Beckhoff_EtherCAT_ESI_EL6070-1xxx.zip
rm Beckhoff_EtherCAT_XML.zip