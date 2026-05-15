FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Cài đặt các thư viện cần thiết cho engine (nếu có)
RUN apt-get update && apt-get install -y \
    libgomp1 \
    && rm -rf /var/lib/apt/lists/*

# Tạo thư mục Engines
RUN mkdir -p /app/Engines

# ✅ COPY CẢ 2 FILE
COPY Engines/pikafish /app/Engines/pikafish
COPY Engines/pikafish.nnue /app/Engines/pikafish.nnue

# --- BẮT ĐẦU KIỂM TRA ---

# 1. Kiểm tra loại file và kiến trúc
RUN file /app/Engines/pikafish

# 2. Kiểm tra các thư viện phụ thuộc (quan trọng nhất)
RUN ldd /app/Engines/pikafish || true

# 3. Thử chạy lệnh 'uci' và xem output
RUN echo "uci" | /app/Engines/pikafish || echo "ERROR: Engine failed to run"

# --- KẾT THÚC KIỂM TRA ---

# Set quyền thực thi cho binary
RUN chmod +x /app/Engines/pikafish

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "XiangqiApi.dll"]