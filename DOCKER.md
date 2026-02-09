# Docker - WorkPlanner

Ten dokument opisuje jak uruchamiać aplikację WorkPlanner w kontenerach Docker lokalnie oraz na serwerze produkcyjnym.

## Architektura

```
┌─────────────────┐         ┌─────────────────┐
│  Blazor WASM    │         │   ASP.NET API   │
│   (nginx)       │────────▶│   (SQLite)    │
│   Port: 7127    │  HTTP   │   Port: 7191  │
└─────────────────┘         └─────────────────┘
         │                           │
         └───────────────────────────┘
                    CORS
```

## Wymagania

- Docker 20.10+
- Docker Compose 2.0+
- 2GB RAM (minimum)

---

## Lokalny Development

### Szybki start

```bash
# 1. Klonowanie repozytorium
git clone <repo-url>
cd WorkPlanner

# 2. Uruchomienie aplikacji
docker-compose up --build

# 3. Otwórz przeglądarkę
# Client: http://localhost:7127
# API:    http://localhost:7191
```

### Dostępne adresy (lokalnie)

| Usługa | URL | Opis |
|--------|-----|------|
| Client | http://localhost:7127 | Blazor WASM UI |
| API | http://localhost:7191 | REST API |
| API Health | http://localhost:7191/health | Status API |

### Przydatne komendy

```bash
# Uruchomienie w tle
docker-compose up -d

# Zatrzymanie
docker-compose down

# Zatrzymanie i usunięcie wolumenów (Baza danych!)
docker-compose down -v

# Przebudowanie obrazów
docker-compose up --build

# Logi
docker-compose logs -f api
docker-compose logs -f client

# Restart pojedynczej usługi
docker-compose restart api
```

### Baza danych (SQLite)

SQLite jest przechowywany w wolumenie Docker:
```
./data/workplanner.db  # Lokalnie (mount)
/app/data/workplanner.db  # W kontenerze
```

**Uwaga:** Baza danych jest trwała między restartami. Usunięcie wolumenu usunie dane:
```bash
# Usuń kontenery i bazę danych
docker-compose down -v
```

---

## Produkcja (Deployment)

### Przygotowanie serwera

1. **Zainstaluj Docker** na serwerze:
```bash
curl -fsSL https://get.docker.com | sh
sudo usermod -aG docker $USER
```

2. **Skopiuj pliki projektu**:
```bash
# Na lokalnej maszynie
scp -r WorkPlanner user@server:/opt/

# Lub przez git
ssh user@server
cd /opt
git clone <repo-url>
```

### Konfiguracja produkcyjna

#### 1. SSL/TLS (HTTPS)

**Opcja A: Let's Encrypt (zalecane)**

```bash
# Zainstaluj certbot
sudo apt install certbot

# Wygeneruj certyfikat
sudo certbot certonly --standalone -d twoja-domena.pl

# Skopiuj certyfikaty
sudo cp /etc/letsencrypt/live/twoja-domena.pl/fullchain.pem nginx/ssl/cert.pem
sudo cp /etc/letsencrypt/live/twoja-domena.pl/privkey.pem nginx/ssl/key.pem
sudo chmod 644 nginx/ssl/*.pem
```

**Opcja B: Własny certyfikat**

```bash
# Umieść certyfikaty w katalogu nginx/ssl/
mkdir -p nginx/ssl
cp twoj-cert.pem nginx/ssl/cert.pem
cp twoj-key.pem nginx/ssl/key.pem
```

#### 2. Edycja nginx.conf

Odkomentuj sekcję HTTPS w `nginx/nginx.conf`:

```nginx
server {
    listen 443 ssl http2;
    server_name twoja-domena.pl;
    
    ssl_certificate /etc/nginx/ssl/cert.pem;
    ssl_certificate_key /etc/nginx/ssl/key.pem;
    # ...
}
```

#### 3. Uruchomienie produkcyjne

```bash
# Na serwerze
cd /opt/WorkPlanner

# Uruchomienie
docker-compose -f docker-compose.prod.yml up -d

# Sprawdzenie statusu
docker-compose -f docker-compose.prod.yml ps
docker-compose -f docker-compose.prod.yml logs -f
```

### Aktualizacja produkcji

```bash
cd /opt/WorkPlanner

# Pobierz nowy kod
git pull

# Przebuduj i uruchom
docker-compose -f docker-compose.prod.yml down
docker-compose -f docker-compose.prod.yml up --build -d

# Wyczyść stare obrazy
docker image prune -f
```

### Backup bazy danych

```bash
# Backup
docker cp workplanner-api:/app/data/workplanner.db backup-$(date +%Y%m%d).db

# Restore
docker cp backup-20240101.db workplanner-api:/app/data/workplanner.db
docker-compose -f docker-compose.prod.yml restart api
```

---

## Konfiguracja CORS

### Lokalnie (docker-compose.yml)

CORS origins są konfigurowane przez zmienne środowiskowe:

```yaml
environment:
  - CORS__Origins__0=http://localhost:7127
  - CORS__Origins__1=http://localhost:5027
```

### Produkcja

W produkcji CORS jest obsługiwane przez nginx reverse proxy (nie bezpośrednio przez API).

---

## Rozwiązywanie problemów

### Błąd: "Connection refused"

```bash
# Sprawdź czy kontenery działają
docker-compose ps

# Sprawdź logi
docker-compose logs api
docker-compose logs client
```

### Błąd CORS w przeglądarce

Upewnij się, że origins są poprawnie skonfigurowane:
```bash
# Sprawdź zmienne środowiskowe API
docker-compose exec api env | grep CORS
```

### Błąd: "Cannot find module"

```bash
# Wyczyść i przebuduj
docker-compose down -v
docker-compose up --build
```

### Porty są zajęte

```bash
# Sprawdź co używa portów
sudo netstat -tulpn | grep -E '7127|7191'

# Zmień porty w docker-compose.yml
ports:
  - "8080:80"  # Zamiast 7127:80
```

---

## Środowiska

### Development (docker-compose.yml)
- Hot reload: Nie (wymaga volume mounts)
- Debug: Możliwe przez porty
- Baza danych: Lokalny mount ./data

### Production (docker-compose.prod.yml)
- Reverse proxy: nginx
- SSL: Włączony
- Autorestart: Tak
- Baza danych: Nazwany wolumen Docker

---

## Zmienne środowiskowe

### API

| Zmienna | Domyślna | Opis |
|---------|----------|------|
| `ASPNETCORE_ENVIRONMENT` | Production | Środowisko |
| `ASPNETCORE_URLS` | http://+:8080 | Adres nasłuchiwania |
| `ConnectionStrings__DefaultConnection` | - | Connection string SQLite |
| `CORS__Origins__0` | - | Dozwolony origin #1 |
| `CORS__Origins__1` | - | Dozwolony origin #2 |

### Client

| Zmienna | Domyślna | Opis |
|---------|----------|------|
| `API_BASE_URL` | http://localhost:7191 | URL API (dla development) |
| `API_INTERNAL_URL` | http://api:8080 | URL API (dla produkcji) |

---

## Struktura plików Docker

```
WorkPlanner/
├── docker-compose.yml           # Development
├── docker-compose.prod.yml      # Produkcja
├── .dockerignore               # Ignorowane pliki
├── WorkPlanner.Api/
│   └── Dockerfile              # Obraz API
├── WorkPlanner.Client/
│   ├── Dockerfile              # Obraz Client (nginx)
│   └── nginx.conf              # Konfiguracja nginx (dev)
└── nginx/
    └── nginx.conf              # Konfiguracja nginx (prod)
```

---

## Wskazówki

1. **Pierwsze uruchomienie** może potrwać dłużej (budowanie obrazów)
2. **Baza danych** jest inicjalizowana automatycznie przy pierwszym uruchomieniu
3. **Default admin**: `admin@workplanner.com` / `Admin123!`
4. **Healthcheck**: API sprawdza się co 30s, client czeka na API

---

## Wsparcie

W przypadku problemów:
1. Sprawdź logi: `docker-compose logs`
2. Sprawdź status: `docker-compose ps`
3. Zweryfikuj konfigurację CORS
4. Upewnij się, że porty nie są zajęte
