# Giai đoạn build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy csproj và restore dependencies
COPY *.csproj .
RUN dotnet restore

# Copy toàn bộ source và build
COPY . .
RUN dotnet publish -c Release -o /app/publish

# Giai đoạn runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# Copy kết quả build từ giai đoạn trên
COPY --from=build /app/publish .

# Expose cổng (Render yêu cầu cổng 8080)
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Chạy app - DÙNG CÚ PHÁP ĐÚNG
ENTRYPOINT ["dotnet", "XiangqiApi.dll"]