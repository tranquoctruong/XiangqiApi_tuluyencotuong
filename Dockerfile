FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# ✅ Cài đặt các công cụ cần thiết để debug
RUN apt-get update && apt-get install -y \
    file \
    binutils \
    libc6 \
    libstdc++6 \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /app/Engines

# Copy engine
COPY Engines/pikafish /app/Engines/pikafish
COPY Engines/pikafish.nnue /app/Engines/pikafish.nnue

# Set quyền thực thi
RUN chmod +x /app/Engines/pikafish

# --- KIỂM TRA ---
# 1. Kiểm tra loại file (sau khi đã cài file)
RUN file /app/Engines/pikafish

# 2. Kiểm tra thư viện phụ thuộc
RUN ldd /app/Engines/pikafish 2>&1 || echo "ldd check done"

# 3. Thử chạy UCI (cách đơn giản hơn)
RUN echo "uci" | /app/Engines/pikafish 2>&1 | head -n 5 || echo "Engine execution failed with exit code: $?"

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "XiangqiApi.dll"]