# How to develop?

For development, use the IDE that matches your taste (personally, i highly
enjoy [Rider](https://www.jetbrains.com/rider/) but you may
use [Visual Studio Community](https://visualstudio.microsoft.com/de/) too).
In there, you just have to open the `.sln` file. Ensure that you have
the [development version of dotnet installed](https://dotnet.microsoft.com/en-us/download).
After opening the `.sln` file, you will be greeted with a project that contains numerous folders.
The backend is located in `source\X39.UnitedTacticalForces.Api` while the frontend is located
in `source\X39.UnitedTacticalForces.WebApp`.

# How to Install?

The installation for this is a two part system,
one is the static SPA front-end (the actual website),
the other one a dynamic back-end (the api the website communicates with).
Hence, follow both steps carefully:

## Back-End

### Linux-SystemD

1. Install postgresql on your machine
2. Log into your admin user using eg. `sudo -u postgres psql` (`\q` will exit the REPL terminal) and create a new user and database on it using the
   following SQL:
   ```postgresql
   CREATE USER username WITH PASSWORD 'password';
   CREATE DATABASE dbname;
   GRANT ALL PRIVILEGES ON DATABASE dbname TO username;
   ```
3. [Install dotnet on your machine](https://learn.microsoft.com/en-us/dotnet/core/install/linux)
4. Create a new folder at `/var/aspnetcore/utf-api`
5. Paste the [latest backend artifacts](https://github.com/X39/UnitedTacticalForces/actions) flat in here
6. Create a new file at `/etc/systemd/system/utf-api.service` with the following contents:
   ```
   [Unit]
   Description=UTF Web API
   
   [Service]
   WorkingDirectory=/var/aspnetcore/utf-api
   ExecStart=/usr/bin/dotnet /var/aspnetcore/utf-api/X39.UnitedTacticalForces.Api.dll
   Restart=always
   # Restart service after 10 seconds if dotnet service crashes:
   RestartSec=10
   KillSignal=SIGINT
   SyslogIdentifier=utf-api
   User=www-data
   Environment=ASPNETCORE_ENVIRONMENT=Production
   Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
   
   [Install]
   WantedBy=multi-user.target
   ```
7. Create a new file at `var/aspnetcore/utf-api/appsettings.secret.json` with the following content:
   ```json
   {
     "ConnectionStrings": {
       "ApiDbContext": "YOUR_POSTGRES_CONNECTION_STRING"
     },
     "Steam": {
       // https://steamcommunity.com/dev/apikey
       "ApiKey": "YOUR_SECRET",
       "Username": "STEAM_USERNAME_ONLY_NEEDED_IF_ANONYMOUS_ONLY_FALSE",
       "Password": "STEAM_PASSWORD_ONLY_NEEDED_IF_ANONYMOUS_ONLY_FALSE"
     },
     "Jwt": {
       "SecretKey": "YOUR_SECRET_KEY"
     }
   }
   ```
8. Check the other `appsettings.json` files present if they match your liking.
   *It is recommended to write changes to the default values into a `appsettings.User.json` file instead of modifying
   the files directly.*
9. Start the API using `systemctl start utf-api`
10. Continue with the front-end installation, then come back here
11. Login to frontend
12. Use `sudo -u postgres psql` again to connect to postgres.
13. Enter `\connect dbname` to connect to the database created earlier
14. Use `INSERT INTO "RoleUser" ("RolesPrimaryKey", "UsersPrimaryKey") VALUES ((SELECT "PrimaryKey" FROM "Roles" WHERE "Identifier" = 'admin'), (SELECT "PrimaryKey" FROM "Users"));`
    to give yourself administrative rights.
    *Note that if more then a single user logged in, the value must be entered manually with the query above. Further users
    can be administrated from the web app*
15. You are done!

## Front-End

### Linux-NGINX with SSL

1. Create a new folder at `/var/www/utf-frontend`
2. Paste the [latest frontent artifacts](https://github.com/X39/UnitedTacticalForces/actions) flat in here
3. Setup SSL certificate for your domain (in this case: `subdomain.yourdomain.xx`)
4. Add a file with the following contents to `/etc/nginx/sites-available/utf.webapp`:
   ```
   # HTTP server configuration
   server {
   listen      80;
   listen [::]:80;
   server_name subdomain.yourdomain.xx;
   
       # Redirect all HTTP traffic to HTTPS
       return 301 https://$host$request_uri;
   }
   
   # HTTPS server configuration
   server {
   listen      443 ssl http2;
   listen [::]:443 ssl http2;
   server_name subdomain.yourdomain.xx;
   
       # SSL settings
       ssl_certificate /path/to/certificate.cer;
       ssl_certificate_key /path/to/certificate-key.key;
       ssl_session_timeout 5m;
       ssl_dhparam /path/to/diffie-hellman-parameter.pem;
       ssl_stapling on;
       ssl_stapling_verify on;
   
       # ASP.NET Core API location
       location /api {
           proxy_pass         http://localhost:5000; # Port where your API is running
           proxy_http_version 1.1;
           proxy_set_header   Upgrade $http_upgrade;
           proxy_set_header   Connection keep-alive;
           proxy_set_header   Host $host;
           proxy_cache_bypass $http_upgrade;
           proxy_set_header   X-Forwarded-For $proxy_add_x_forwarded_for;
           proxy_set_header   X-Forwarded-Proto $scheme;
   
           # Blazor Server settings (if needed)
           proxy_buffering           off;
           proxy_buffer_size         16k;
           proxy_buffers             4 16k;
           proxy_busy_buffers_size   24k;
           proxy_max_temp_file_size 0;
        }
   
        # Blazor WebAssembly app location
        location / {
            root /var/www/utf-frontend/wwwroot;
            try_files $uri $uri/ /index.html =404;
   
            # WebSockets settings (if needed)
            include /etc/nginx/mime.types;
            add_header Cache-Control no-cache always;
        }
   }
   ```
5. Create a link using `ln /etc/nginx/sites-enabled/utf-webapp /etc/nginx/sites-available/utf.webapp`
6. Reload your nginx server by using `service nginx reload`