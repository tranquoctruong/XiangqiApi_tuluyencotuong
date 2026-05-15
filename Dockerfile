FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Tạo thư mục Engines
RUN mkdir -p /app/Engines

# ✅ COPY CẢ 2 FILE
COPY Engines/pikafish /app/Engines/pikafish
COPY Engines/pikafish.nnue /app/Engines/pikafish.nnue

# Set quyền thực thi cho binary
RUN chmod +x /app/Engines/pikafish

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "XiangqiApi.dll"]