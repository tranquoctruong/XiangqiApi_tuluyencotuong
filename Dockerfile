# 1. Giai đoạn build: Dùng SDK để biên dịch code
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /app

# Copy file .csproj và restore dependencies (tận dụng cache của Docker)
COPY *.csproj ./
RUN dotnet restore

# Copy toàn bộ source code và build
COPY . ./
RUN dotnet publish -c Release -o out

# 2. Giai đoạn runtime: Chỉ chạy, không cần SDK (ảnh nhẹ hơn)
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy kết quả build từ giai đoạn trên sang
COPY --from=build /app/out .

# Quan trọng: Khai báo cổng mà API lắng nghe (Render yêu cầu cổng 8080) [citation:2]
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Khởi động API
ENTRYPOINT ["dotnet", "XiangqiApi.dll"] # Thay YourApiName bằng tên file .dll của bạn