FROM mcr.microsoft.com/dotnet/runtime:6.0-alpine AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src
COPY ["GrpcPing.csproj", "./"]
RUN dotnet restore "GrpcPing.csproj"
COPY . .
WORKDIR "/src/"
RUN dotnet build "GrpcPing.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "GrpcPing.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
#RUN sh -c 'echo "openssl_conf = openssl_init" | cat - /etc/ssl/openssl.cnf.dist > /etc/ssl/openssl.cnf && echo -e  "[openssl_init]\nssl_conf = ssl_sect\n\n[ssl_sect]\nsystem_default = system_default_sect\n\n[system_default_sect]\nMaxProtocol = TLSv1.2" >> /etc/ssl/openssl.cnf'
ENTRYPOINT ["dotnet", "GrpcPing.dll"]
