# Siber Vatan Kayıt Botu

Bu proje, Telegram gruplarında kullanıcıların gerçek isim ve soyisimleri ile kayıt olmalarını sağlayan bir bottur. Kayıtlar grup bazlı tutulur ve Redis üzerinde saklanır.

## Özellikler

- **Grup Bazlı Kayıt**: Kullanıcılar her grup için ayrı ayrı kayıt olabilir.
- **Güvenli Doğrulama**: Kayıt linki sadece ilgili gruptan tıklandığında çalışır ve kullanıcının o gruba üye olup olmadığını kontrol eder.
- **CSV Dışa Aktarma**: Yöneticiler, gruptaki kayıtlı ve kayıt olmayan kullanıcıların listesini CSV formatında alabilir.
- **Kullanıcı Sorgulama**: Yöneticiler, bir kullanıcının hangi gruplarda hangi isimle kayıtlı olduğunu sorgulayabilir.

## Kurulum Adımları

### 1. Telegram Botunu Oluşturma
1. Telegram'da **[@BotFather](https://t.me/BotFather)** kullanıcısını bulun.
2. `/newbot` komutunu gönderin.
3. Botunuz için bir isim ve kullanıcı adı (sonu `bot` ile biten) belirleyin.
4. BotFather size bir **HTTP API Token** verecektir. Bu tokeni kaydedin.

### 2. Gereksinimler
- Python 3.11 veya üzeri
- Redis Sunucusu (Yerel veya uzak sunucu)

### 3. Projeyi Hazırlama
Projeyi bilgisayarınıza indirin ve ilgili klasöre gidin:

```bash
cd SiberVatan
```

Gerekli Python kütüphanelerini yükleyin:

```bash
pip install -r requirements.txt
```

### 4. Ayarlar (.env)
`.env.example` dosyasının adını `.env` olarak değiştirin ve içeriğini düzenleyin:

```ini
BOT_TOKEN=123456789:ABCdefGHIjklMNOpqRstUVwxyz # BotFather'dan aldığınız token
ADMIN_ID=123456789,987654321 # Yönetici ID'leri (virgülle ayırarak birden fazla ekleyebilirsiniz)
REDIS_HOST=localhost
REDIS_PORT=6379
REDIS_DB=0
```

> **Not:** Kendi Telegram ID'nizi öğrenmek için [@userinfobot](https://t.me/userinfobot) kullanabilirsiniz.

### 5. Botu Çalıştırma
Botu başlatmak için şu komutu kullanın:

```bash
python main.py
```

Veya Docker ile:

```bash
docker build -t sibervatan .
docker run -d --env-file .env --network host sibervatan
```

## Kullanım Kılavuzu

### Botu Gruplara Ekleme
1. Oluşturduğunuz botu yönetmek istediğiniz Telegram grubuna ekleyin.
2. Botun mesajları okuyabilmesi ve üyeleri kontrol edebilmesi için **Yönetici (Admin)** yapmanızı öneririz (gerekli izinler: Mesajları görme, Kullanıcıları davet etme).

### Komutlar

Aşağıdaki komutlar sadece `.env` dosyasında tanımlanan **ADMIN_ID** kişiler tarafından kullanılabilir.

#### `/register` (Sadece Grupta)
Bu komutu grubun içinde gönderin.
- Bot, gruba bir "Kayıt Ol" butonu içeren mesaj gönderir.
- Üyeler bu butona tıkladığında botun özel mesaj kutusuna yönlendirilir.
- Bot, kullanıcıdan Ad ve Soyadını yazmasını ister.
- Kullanıcı bilgisini girdikten sonra sadece o grup için kaydı tamamlanır.

#### `/users` (Özel Mesaj veya Grup)
Bu komut ile kayıtlı grupların listesini görebilirsiniz.
- Bot size veritabanında kayıtlı olan grupları butonlar halinde listeler.
- Bir gruba tıkladığınızda, o gruptaki **Toplam Üye**, **Kayıtlı Üye** ve **Kayıt Olmayan Üye** sayısını içeren bir mesaj ve detaylı bir **CSV dosyası** gönderir.
- CSV dosyası içinde: User ID, Telegram Adı, Kullanıcı Adı ve Kayıtlı Gerçek İsim bilgileri yer alır.

#### `/info` (Özel Mesaj veya Grup)
Bir kullanıcının bilgilerini sorgulamak için kullanılır.
- **Kullanım 1 (Yanıtla):** Grupta bir kullanıcının mesajını `/info` yazarak yanıtlayın.
- **Kullanım 2 (ID ile):** `/info 123456789` şeklinde kullanıcı ID'si yazarak gönderin.
- Bot, o kullanıcının Telegram bilgilerini ve **hangi gruplarda hangi isimle kayıtlı olduğunu** listeler.

---
**İyi kullanımlar!**
