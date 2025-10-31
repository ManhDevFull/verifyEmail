# -------------------------------
# Stage 1: Build ứng dụng
# -------------------------------
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy file project và restore dependencies
COPY verify.csproj ./
RUN dotnet restore "verify.csproj"

# Copy toàn bộ source code và build ở chế độ Release
COPY . .
RUN dotnet publish "verify.csproj" -c Release -o /app/publish

# -------------------------------
# Stage 2: Runtime (chạy ứng dụng)
# -------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app

# Copy kết quả build từ stage trước
COPY --from=build /app/publish .

# Lệnh chạy ứng dụng .NET
ENTRYPOINT ["dotnet", "verify.dll"]