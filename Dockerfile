# --- Derleme aşaması: SDK yalnızca burada kullanılır ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Önce yalnızca proje dosyasını kopyala: bağımlılıklar değişmedikçe
# restore katmanı önbellekten gelir ve derleme hızlanır.
COPY WalletApi/WalletApi.csproj WalletApi/
RUN dotnet restore WalletApi/WalletApi.csproj

COPY WalletApi/ WalletApi/
RUN dotnet publish WalletApi/WalletApi.csproj -c Release -o /app --no-restore

# --- Çalışma aşaması: SDK ve kaynak kod imaja girmez ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

COPY --from=build /app .

# Kök kullanıcı olarak çalıştırma: konteyner ele geçirilirse yetkiyi sınırlar.
USER $APP_UID

EXPOSE 8080

ENTRYPOINT ["dotnet", "WalletApi.dll"]
