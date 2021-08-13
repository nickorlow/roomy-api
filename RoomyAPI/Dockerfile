FROM mcr.microsoft.com/dotnet/sdk:5.0 AS build-env
WORKDIR /app

# Copy csproj and restore as distinct layers
COPY *.csproj ./

# Copy everything else and build
COPY . ./
RUN dotnet publish -c Release -o out

# Build runtime image
FROM mcr.microsoft.com/dotnet/aspnet:5.0
WORKDIR /app
COPY --from=build-env /app/out .
EXPOSE 80
ENV ASPNETCORE_URLS=http://*:80

# Get from Azure
ARG DB_ADDRESS_ARG
ARG DB_USERNAME_ARG
ARG DB_PASSWORD_ARG
ARG DB_NAME_ARG

# Set environment variables
ENV DB_ADDRESS=$DB_ADDRESS_ARG
ENV DB_USERNAME=$DB_USERNAME_ARG
ENV DB_PASSWORD=$DB_PASSWORD_ARG
ENV DB_NAME=$DB_NAME_ARG

ENTRYPOINT ["dotnet", "RoomyAPI.dll"]
