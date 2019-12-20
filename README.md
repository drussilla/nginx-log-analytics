# nginx-log-analytics
Generate views report in the console based on nginx access log files.  

![alt text](https://github.com/drussilla/nginx-log-analytics/workflows/Build%20and%20Test%20Solution/badge.svg "Build status")

![alt text](https://github.com/drussilla/nginx-log-analytics/raw/master/example.PNG "Output example")

# NGINX configuration
1. Edit `/etc/nginx/nginx.conf` (might be in the different location in your system) and add the following log format in to `http` section:

```
 log_format custom '$time_local | $remote_addr | $request | $status | $body_bytes_sent | $http_referer | $http_user_agent | $request_time';
```
2. Edit your website configuration and specify previously defined log format for the access logs:

```
access_log /var/ivanderevianko.com/logs/access.log custom;
```

# Build nginx-log-analytics

## Prerequesites

- .Net core 3.1 SDK https://dotnet.microsoft.com/download/dotnet-core/3.1

## Publish

Use `publish.cmd` or the following command to create an `linux-x64` executable:
```
dotnet publish src/NginxLogAnalytics/NginxLogAnalytics.csproj -c Release -o Release -r linux-x64 --no-self-contained
```

# Run nginx-log-analytics

## Configuration
Edit `config.json` and specify the correct log folder (`LogFilesFolderPath` property), in this examples it should be the following:
```javascript
{
  "LogFilesFolderPath": "/var/ivanderevianko.com/logs/",
  "CrawlerUserAgentsFilePath": "ingore-user-agents.txt",
  "ExcludeContentFilePath": "not-content-list.txt"
}
```

## Run
Go to release folder: `cd Release`  
make file executable: `chmod +x NginxLogAnalytics`  
and run it: `./NginxLogAnalytics`  
