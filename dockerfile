FROM debian:latest

RUN apt-get update
RUN apt-get upgrade -y

RUN mkdir -p /mnt/datenstruktur
RUN mkdir -p /mnt/anwendungen

ENV DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1

COPY bin/Debug/netcoreapp3.1/linux-x64/publish/isci.ethercatmaster /usr/local/bin

ENTRYPOINT ["/usr/local/bin/isci.ethercatmaster"]