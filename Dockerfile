FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Cài đặt thêm dependencies
RUN apt-get update && apt-get install -y \
    wget \
    unzip \
    libgomp1 \
    libc6 \
    libstdc++6 \
    && rm -rf /var/lib/apt/lists/*

# Tạo thư mục Engines
RUN mkdir -p /app/Engines

# Tải bản SSE41 (tương thích nhất)
RUN wget -O /tmp/pikafish.zip https://github.com/official-pikafish/Pikafish/releases/download/pikafish-12.2/pikafish-linux-sse41-popcnt.zip && \
    unzip /tmp/pikafish.zip -d /app/Engines/ && \
    chmod +x /app/Engines/pikafish-sse41-popcnt && \
    mv /app/Engines/pikafish-sse41-popcnt /app/Engines/pikafish && \
    rm /tmp/pikafish.zip

# Kiểm tra file
RUN file /app/Engines/pikafish && \
    ldd /app/Engines/pikafish || true

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Debug: chạy thử engine
RUN /app/Engines/pikafish --help || echo "Engine test complete"

ENTRYPOINT ["dotnet", "XiangqiApi.dll"]