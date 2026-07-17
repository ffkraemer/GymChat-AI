# GymChat AI — Roteiro de Arranque

Guia rápido para pores a aplicação a correr do zero, sempre que voltares a trabalhar nela.
Segue os passos por ordem — cada um depende do anterior.

---

## ✅ Checklist rápido (se já sabes o processo de cor)

- [ ] Docker Desktop aberto
- [ ] `docker compose up -d` (raiz do repo)
- [ ] `dotnet run --project src\GymChatAI.Api`
- [ ] `ngrok http 5277` (**noutra janela**)
- [ ] Confirmar se o URL do ngrok mudou → se sim, atualizar Callback URL no Meta
- [ ] `GET /health`, `GET /health/whatsapp`, `GET /health/ai` → todos `"ok"`
- [ ] (opcional) `cd frontend && npm run dev` se fores usar o Portal de Administração

---

## Passo 1 — Docker Desktop

Abre a aplicação **Docker Desktop** e espera que o ícone fique verde/estável (não basta o processo arrancar, tem de terminar de inicializar).

## Passo 2 — Subir o SQL Server

Na **raiz do repositório**:
```powershell
docker compose up -d
```

Confirma que está saudável:
```powershell
docker ps
```
Deve mostrar `gymchatai-sqlserver` com estado `healthy` (pode demorar uns 10-20s a passar de `starting` para `healthy` na primeira vez).

> Não uses `docker compose down -v` a menos que queiras mesmo apagar os dados — o `-v` remove o volume onde a base de dados vive. `docker compose down` (sem `-v`) ou simplesmente deixar os containers a correr são seguros.

## Passo 3 — Arrancar o backend

```powershell
dotnet run --project src\GymChatAI.Api
```
(ou usa o ▶ Play do Visual Studio)

Confirma no log:
```
GymChat AI started using SQL Server persistence (authentication enabled).
```

Se aparecer `"falling back to in-memory persistence"` em vez disso, a connection string não foi encontrada — confirma os `user-secrets`:
```powershell
cd src\GymChatAI.Api
dotnet user-secrets list
```

## Passo 4 — ngrok (numa janela de terminal **separada**, deixa esta a correr)

```powershell
ngrok http 5277
```

Confirma a linha `Forwarding`:
```
Forwarding    https://xxxx-xxxx.ngrok-free.dev -> http://localhost:5277
```

### ⚠️ O URL muda sempre que reinicias o ngrok (plano grátis)

Se este URL for **diferente** do que já tinhas configurado da última vez, tens de atualizar no Meta:

1. `developers.facebook.com` → a tua app → **Use cases → Customize → Basic setup → Step 2. Production setup**
2. **Configure Webhooks** → **Callback URL**: cola o novo URL + `/webhooks/whatsapp/` no fim
3. **Verify Token**: o mesmo de sempre (não muda)
4. **Verify and Save**

## Passo 5 — Confirmar que tudo está saudável, antes de testares

```
GET http://localhost:5277/health
GET http://localhost:5277/health/whatsapp
GET http://localhost:5277/health/ai
```

Todos devem devolver `"status": "ok"` (ou `"healthy"` no caso do `/health` geral). Se algum disser `"expired"` ou `"error"`, resolve isso **antes** de tentares testar pelo WhatsApp — poupa tempo a diagnosticar o sítio errado.

| Se `/health/whatsapp` falhar | Se `/health/ai` falhar |
|---|---|
| Token expirado → gera um novo (permanente, via System User) | Chave inválida, revogada, ou (Gemini free) limite de taxa temporário |

## Passo 6 — (Opcional) Portal de Administração

```powershell
cd frontend
npm run dev
```
Abre `http://localhost:5173`. Login: `admin@demo.gymchat.ai` / `GymChat!Demo123`.

## Passo 7 — Testar

Envia uma mensagem de WhatsApp para o teu número de teste. Acompanha em paralelo:
- O terminal do `dotnet run` (logs do processamento)
- `http://127.0.0.1:4040` (Inspector do ngrok — confirma que o pedido chegou)

---

## 🔧 Referência rápida de problemas já resolvidos

| Sintoma | Causa provável | Solução |
|---|---|---|
| Nada chega ao ngrok Inspector | URL do ngrok mudou, ou não está a correr | Passo 4 |
| Chega ao ngrok mas app não recebe | App não está a correr, ou porta errada | Confirma Passo 3 |
| `"No gym configured for WhatsApp phone number id"` | `DemoPhoneNumberId` não bate certo, ou processo antigo ainda na porta | Confirma `appsettings`/secrets; mata processos antigos na porta 5277 |
| Erro 401 do WhatsApp ao enviar | Token expirado | `GET /health/whatsapp`; gera token permanente (System User) |
| `"AI assistant unavailable"` | Chave de IA inválida, ou limite de taxa (comum no Gemini free) | `GET /health/ai`; olha a linha de erro completa no log, acima da mensagem resumida |
| `"Business account is restricted from messaging users in this country"` | Restrição Brasil/Indonésia entre países diferentes | Usa destinatário de país não-restrito |
| CORS bloqueado no frontend | Falta `app.UseCors(...)` no backend | Confirma `Program.cs` |
| `Invalid object name 'X'` no SQL | Falta aplicar migração nova | `dotnet ef migrations add ... && dotnet run` (o `MigrateAsync()` aplica sozinho) |
| Erro no primeiro request a um endpoint gym-scoped | `GymScopeFilter` a bloquear (gymId da rota ≠ claim do token) | Confirma que estás a usar o `gymId` certo para o utilizador autenticado |

---

## Credenciais de referência

| O quê | Valor |
|---|---|
| Login do Portal de Administração | `admin@demo.gymchat.ai` / `GymChat!Demo123` |
| Password do SQL Server (dev) | `Your_password123` (definida no `docker-compose.yml`) |
| Onde renovar o token do WhatsApp | `business.facebook.com` → System Users → Generate Token (Never expire) |
| Onde renovar a chave do Gemini | `aistudio.google.com/apikey` |

---

*Este documento é só para desenvolvimento local. Em produção, o arranque seria gerido por infraestrutura própria (não Docker Desktop/ngrok manuais).*
