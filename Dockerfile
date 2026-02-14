FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
COPY WillhabenScrap/WillhabenScrap.csproj WillhabenScrap/
RUN dotnet restore WillhabenScrap/WillhabenScrap.csproj
COPY . .
RUN dotnet publish WillhabenScrap/WillhabenScrap.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/runtime:10.0-preview
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "WillhabenScrap.dll"]