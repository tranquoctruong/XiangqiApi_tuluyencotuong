FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY *.csproj .
RUN dotnet restore

COPY . .
RUN dotnet publish -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app

# ✅ Cài tất cả thư viện cần thiết cho Pikafish
RUN apt-get update && apt-get install -y \
    libatomic1 \
    libstdc++6 \
    libgcc-s1 \
    libc6 \
    && rm -rf /var/lib/apt/lists/*

RUN mkdir -p /app/Engines

# Copy engine
COPY Engines/pikafish /app/Engines/pikafish
COPY Engines/pikafish.nnue /app/Engines/pikafish.nnue

RUN chmod +x /app/Engines/pikafish

# Test engine
RUN echo "uci" | /app/Engines/pikafish 2>&1 | head -5

COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "XiangqiApi.dll"]