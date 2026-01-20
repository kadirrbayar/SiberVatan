# Siber Vatan Telegram Grup Yönetim Botu 🛡️

Bu bot, Telegram gruplarını yönetmek, moderasyon sağlamak ve kullanıcı etkileşimini artırmak için geliştirilmiş gelişmiş bir bottur.

## 📝 Mesaj Formatı ve HTML Kullanımı

Bot üzerindeki tüm mesajlar **HTML** formatını destekler. Aşağıdaki etiketleri kullanabilirsiniz:

- **Kalın:** `<b>Kalın</b>`
- **İtalik:** `<i>İtalik</i>`
- **Altı Çizili:** `<u>Altı Çizili</u>`
- **Üstü Çizili:** `<s>Silik</s>`, `<strike>Üstü Çizili</strike>`
- **Kod:** `<code>Kod</code>`, `<pre>Blok Kod</pre>`
- **Link:** `<a href="https://google.com">Google</a>`
- **Mention:** `<a href="tg://user?id=123456">Kullanıcı Etiketi</a>`
- **Spoiler:** `<span class="tg-spoiler">Gizli Mesaj</span>`, `<tg-spoiler>Spoiler</tg-spoiler>`
- **Alıntı:** `<blockquote>Alıntı</blockquote>`
- **Genişletilebilir Alıntı:** `<blockquote expandable>Uzun Alıntı...</blockquote>`

### 🔘 Mesajlara Buton Ekleme

Butonlar `{}` (süslü parantez) içine alınarak tanımlanır. Her süslü parantez bir **satırı** temsil eder.

- **Tek Satırda Yan Yana Buton Ekleme:**
  `{[Buton 1](link1) [Buton 2](link2)}`
  *(Araya boşluk koyarak yan yana ekleyebilirsiniz)*

- **Alt Alta Buton Ekleme:**
  `{[Üst Buton](link)} {[Alt Buton](link)}`
  *(Yeni bir süslü parantez açarak alt satıra geçebilirsiniz)*

- **Karışık Buton Ekleme:**
  `{[A](l) [B](l)} {[C](l)}`
  *(A ve B yan yana, C onların altında)*

---

## 🧩 Değişkenler (Placeholders)

Hoşgeldin mesajlarında (`/setwelcome`) aşağıdaki değişkenleri kullanabilirsiniz:

| Değişken | Açıklama |
| :--- | :--- |
| `$name` | Kullanıcının adı (Tıklanabilir Mention olarak) |
| `$username` | Kullanıcı adı (Örn: ahmet123) |
| `$id` | Kullanıcının Telegram ID'si |
| `$language` | Kullanıcının dil kodu (Örn: tr) |
| `$title` | Grubun adı |

**Örnek Kullanım:**
`Merhaba $name, $title grubuna hoş geldin! ID'n: $id`

---

## 👋 Hoşgeldin Mesajı Ayarlama (/setwelcome)

Gruba yeni biri katıldığında atılacak mesajı ayarlar. Metin, resim, video veya GIF kullanabilirsiniz.

**Kullanım:**
1. **Sadece Metin:**
   `/setwelcome Merhaba $name, aramıza hoş geldin!`

2. **Medya ile (Fotoğraf/Video/GIF):**
   - Gruba bir fotoğraf/video gönderin.
   - O medyayı yanıtlayarak (Reply) komutu yazın:
     `/setwelcome Merhaba $name, kuralları okumayı unutma!`

3. **Butonlu Örnek:**
   `/setwelcome Hoş geldin $name! {[Kurallar](https://t.me/kural_linki) [Kanalımız](https://t.me/kanal_linki)}`

💡 **İpucu:** Ayarladığınız mesajın nasıl göründüğünü test etmek için `/welcome` komutunu kullanabilirsiniz.

---

## ⚡ Extra (Özel Komut) Sistemi

Grupta sık sorulan sorular veya hazır cevaplar için `#hashtag` komutları oluşturabilirsiniz.

- **Ekleme:** `/extra #komutadi Cevap metni`
  *Örn:* `/extra #kurallar Grup kuralları şunlardır...`
- **Silme:** `/extradel #komutadi`
- **Listeleme:** `/extralist`

**İpucu:** Extra komutlarına da buton ve HTML ekleyebilirsiniz.

---

## 📢 Broadcast (Duyuru) Sistemi

Botun bulunduğu **tüm kayıtlı gruplara** mesaj göndermek için kullanılır. (Sadece Geliştiriciler)

- **Düz Mesaj:** `/broadcast Sistemsel bakım yapılacaktır.`
- **Medyalı Mesaj:** Bir fotoğrafa reply atarak `/broadcast` yazarsanız, o mesaj tüm gruplara iletilir.
- **Butonlu:** `/broadcast Yeni özellikler eklendi! {[Detaylar](https://site.com)}`

---

## 📩 Özel Mesaj Gönderme (/sendmsg)

Belirli bir kişiye veya gruba (ID kullanarak) mesaj göndermek için kullanılır. (Sadece Geliştiriciler)

- **Kullanım:** `/sendmsg <TargetID> <Mesaj>`
- **Tek Kişiye:** `/sendmsg 123456789 Merhaba nasılsın?`
- **Gruba:** `/sendmsg -1001234567890 Duyuru: Yarın bakım var.`
- **Medya/Buton:** Broadcast komutundaki gibi reply atarak veya HTML/Buton formatını kullanarak gönderim yapılabilir.

---

## ⚙️ Yönetim Paneli (/menu)

Grup ayarlarını yönetmek için grupta `/menu` yazın. Bot size özelden (PM) bir panel açacaktır.

### Menü Başlıkları

#### 1. General (Genel Ayarlar)
Bu menüden grubunuzun temel davranışlarını ayarlayabilirsiniz:

- **Hoşgeldin Mesajı:**
  - ✅ **Aktif:** Gruba yeni biri katıldığında, `/setwelcome` ile ayarladığınız mesaj gönderilir.
  - ⛔ **Kapalı:** Yeni gelenlere mesaj gönderilmez.

- **Hoşgeldin Mesajını Sil:**
  - ✅ **Aktif:** Yeni bir üye katıldığında, eski hoşgeldin mesajı silinir (Sohbet temizliği için).
  - ⛔ **Kapalı:** Eski mesajlar silinmez.

- **Tüm #Notları Gör (/extralist):**
  - 👥 **Herkes:** Gruptaki tüm üyeler `/extralist` komutunu kullanabilir.
  - 👤 **Sadece Admin:** Sadece yöneticiler not listesini görebilir.

- **#Notlar Kullanımı (/extra):**
  - 👥 **Herkes:** `#not` şeklinde çağrılan notları herkes kullanabilir.
  - 👤 **Sadece Admin:** Notları sadece yöneticiler çağırabilir.

- **Kullanıcı Kayıt:**
  - ✅ **Aktif:** Kayıtlı olmayan kullanıcılar grupta mesaj gönderirse, bot onları uyarır ve kaydolmalarını ister.
  - ⛔ **Kapalı:** Kayıt zorunluluğu yoktur.

- **Mesaj İletme Yasağı:**
  - ✅ **Aktif:** Kanallardan veya başka yerlerden yönlendirilen (forward) mesajlar yasaklanır.
  - ⛔ **Kapalı:** İletilen mesajlara izin verilir.

- **İletim Yasağı Aksiyonu:**
  - Yasaklı bir iletim yapıldığında ne olacağını belirler (Sil, Uyar, Sustur, Yasakla vb.).

#### 2. Anti Spam & Medya Ayarları
Belirli medya türlerini yasaklayabilir veya izin verebilirsiniz.
- **Medya Türleri:** Fotoğraf, Video, Ses, Sesli Mesaj, Sticker, Anket, Konum, Kişi, Link, APK vb.
- **Medya Aksiyonu:** Yasaklı bir medya gönderildiğinde ne yapılsın? (Sil, Uyar, Sustur, Yasakla vb.)

#### 3. Anti Mesaj Uzunluğu
Mesaj uzunluklarını kontrol altında tutar.
- **Maksimum Karakter:** Bir mesajın en fazla kaç karakter olabileceğini belirler (Örn: 4000).
- **Maksimum Satır:** Bir mesajın en fazla kaç satırdan oluşabileceğini belirler.
- **Aksiyon:** Kurallar ihlal edildiğinde ne yapılsın? (Sil, Uyar, Sustur, Yasakla vb.)

#### 4. Flood (Spam Koruması)
- **AntiFlood:** Flood korumasını açar/kapatır.
- **MaxFlood:** Peş peşe kaç mesaja izin verileceği. (5 saniye içinde üst üste kaç mesaj atılabilir)
- **Action:** Flood yapan kullanıcıya ne yapılsın? (Mute/Kick/Ban)

#### 5. Uyarı Ayarları
- **MaxWarns:** Kaç uyarıda ceza verilsin (Örn: 3).
- **WarnAction:** Limit dolunca ne yapılsın? (Kick/Ban/Mute).

### 🏷️ Aksiyon İkonları ve Anlamları
Menülerde gördüğünüz ikonlar şu anlama gelir:

| İkon | Anlamı | Açıklama |
| :---: | :--- | :--- |
| 👟 | **Kick** | Kullanıcı gruptan atılır (Tekrar girebilir). |
| 🔨 | **Ban** | Kullanıcı gruptan yasaklanır. (Tekrar giremez. Siz yasağı kaldırana kadar.) |
| ⏰ | **TempBan** | Kullanıcı geçici olarak yasaklanır (30 dk). |
| ⚠️ | **Warn** | Kullanıcıya uyarı verilir. (Uyarı limitleri uyarı ayarları menüsünden yapılabi̇lir.) |
| 🔇 | **Mute** | Kullanıcı susturulur. (Siz tekrar konuşmasına izin verene kadar.) |
| ✅ | **Allowed** | Eyleme/Medyaya izin verilir. |
| 🚫 | **Blocked** | Eylem/Medya engellenir. |

---

## 🛡️ Moderasyon İşlemleri

**Otomatik Butonlar:**
Bot bir işlem yaptığında (Ban/Mute/Warn gibi), adminlerin işlemi geri alabilmesi için mesajın altına buton ekler:
- `✅ Yasağı Kaldır`
- `🗣 Sesi Aç`
- `⚠️ Uyarıyı Kaldır`

---

##  Kayıt Sistemi (/register & /users)

Kullanıcıların gruplara kayıt olmasını ve bu kayıtların yönetilmesini sağlayan sistemdir.

#### 1. Kayıt Başlatma (/register)
Grup yöneticisi bu komutu grupta çalıştırır.
- Bot gruba **"Kayıt Ol"** butonu içeren bir mesaj gönderir.
- Kullanıcılar butona tıkladığında botun özel mesajına yönlendirilir.
- **Ad Soyad** bilgisi istenir.
- **Yüz Yüze Eğitim:** Kullanıcıya eğitime katılıp katılmayacağı sorulur (Katılıyorum / Katılmıyorum / Belirsiz).
- Seçim yapıldıktan sonra kayıt tamamlanır.

#### 2. Kayıtları Listeleme (/users)
Sadece geliştiriciler (veya yetkili kişiler) kullanabilir.
- Komut çalıştığında kayıtlı tüm gruplar listelenir.
- Bir grup seçildiğinde, o gruptaki kayıtlı üyelerin listesi (Ad, ID, Katılım Durumu) **CSV formatında** oluşturulur ve size gönderilir.

💡 **İpucu:** Kayıt butonunu manuel olarak başka mesajlara (örneğin Welcome mesajına) eklemek isterseniz şu link formatını kullanabilirsiniz:
`https://t.me/BotKullaniciAdi?start=register_GrupID`
*(Grup ID'sini `/id` veya `/users` listesinden öğrenebilirsiniz)*

---

## 🐳 Kurulum (Docker)

1. Repo'yu klonlayın.
2. `.env` dosyasını düzenleyin.
3. Çalıştırın:
```bash
docker-compose up -d --build
```

---

## 🛠️ Manuel Kurulum ve Geliştirme (Local)

Geliştiriciler için proje kurulum adımları:

### Gereksinimler
- .NET 8.0 SDK
- Redis Server (Localhost:6379)

### Kurulum Adımları
1. **Redis'i Başlatın:** Yerel makinenizde Redis servisinin çalıştığından emin olun.
2. **Ayarları Yapılandırın:**
   - `.env` dosyasını oluşturun (örnek dosyadan kopyalayabilirsiniz).
   - İçerisindeki `HELP_API_KEY`, `ADMIN_USER_IDS` ve Redis ayarlarını düzenleyin.
3. **Bağımlılıkları Yükleyin:**
   ```bash
   dotnet restore
   ```
4. **Projeyi Çalıştırın:**
   ```bash
   dotnet run
   ```
   *(Geliştirme modunda anlık değişiklikleri görmek için `dotnet watch` kullanabilirsiniz)*
