# SNUS Projekat - Dokumentacija

## 1. Opis projekta
Popuni AI

## 2. Arhitektura sistema

Arhutektura sistema je bazirana na mikroservisima, gde svaki servis ima jasno definisanu funkcionalnost i odgovornosti. Glavni servisi uključuju:
- **API Gateway**: Služi kao ulazna tačka za sve zahteve klijenata i prosleđuje ih odgovarajućim mikroservisima.
- **Ingestion Service**: Odgovoran za prikupljanje i sigurno skladištenje podataka.
- **Concensus Service**: Implementira algoritme konsenzusa za proveru kvaliteta senzora
- **Sensor Management Service**: Upravljanje i konfiguracija senzora.
- **Notification Service**: Slanje obaveštenja korisnicima o statusu senzora i sistemu.
- **Analytics Service**: Analiza prikupljenih podataka i generisanje izveštaja.
- **Auth Service**: Odgovoran za autentifikaciju i autorizaciju klijenata.

### 2.1. Api Gateway
Ovaj servis služi kao ulazna tačka za sve zahteve klijenata. On prosleđuje zahteve odgovarajućim mikroservisima.

Koristi se i kao registar servisa, omogućavajući dinamičko otkrivanje servisa i balansiranje opterećenja.

Sadrži sigurnosne mehanizme rate limiting-a i autetifikacije.

### 2.2. Ingestion Service
Ovaj servis je odgovoran za prihvatanje vrednosti sa senzora i njihovo skladištenje. Skladištenje treba biti otporno na otkaze i omogućiti skalabilnost pri velikom broju senzora.

Da bi se smanjilo opterećenje na bazu podataka, koristi se batch obrada podataka. Da bi se osigurala pouzdanost, podaci se pre upisa u batch upisuju i u Write-Ahead Log (WAL).
Za primarnu bazu podataka koristimo time-series bazu podataka, kao što je InfluxDB, koja je optimizovana za skladištenje i upite vremenskih serija podataka.

Batch skladištenje je implementirano in-memory, sa konfigurabilnim uslovom flush-a. Postoji nekoliko instanci batch skladišta sa mehanizmom rotacije, kako bi se osigurala dostupnost i skalabilnost. Pri flush-u, batch skladište postaje read-only, a novi podaci se upisuju u sledeće batch skladište. Nakon uspešnog upisa u bazu, prethodno batch skladište se prazni i ponovo koristi, a zapis u WAL se briše.

Nakon uspešnog upisa u WAL, klijentu se šalje potvrda o prijemu podataka, čime se osigurava da su podaci sigurno primljeni i da će biti obrađeni i kreira se novi pub-sub događaj koji obaveštava ostale servise o novim podacima.

Servis se bavi validacijom svakog zahteva proverom timestampa i ID-a poruke koja mu pristiže kako bi se zaštitio od replay napada.

### 2.3. Concensus Service
Ovaj servis implementira algoritme konsenzusa za proveru kvaliteta senzora. Koristi BFT algoritam. Ovaj servis prima podatke od Ingestion servisa i vrši proveru kvaliteta senzora na osnovu definisanih pravila i algoritama konsenzusa. Na osnovu rezultata provere, servis može označiti senzore kao pouzdane ili nepouzdane, što utiče na dalju obradu podataka i obaveštavanje korisnika.

Za potrebe implementacije BFT algoritma, koristi se in-memory skladište za čuvanje trenutnog stanja koje uključuje broj novih vrednosti i trenutna srednja vrednost po senzoru. 

Algoritam konsenzusa se pokreće periodično. Na svakom pokretanju, servis trenutno stanje pretvara u read-only, a novi podaci se upisuju u novu instancu in-memory skladišta. Nakon završetka algoritma, rezultati se upisuju u bazu podataka, a prethodno stanje se briše.

Baza podataka koristi se za čuvanje istorijskih rezultata koncenzusa. Koristi se ralaciona baza podataka, kao što je PostgreSQL, koja omogućava efikasno skladištenje i upite nad istorijskim podacima.

Ukoliko senzor promeni kvalitet, kreira se novi pub-sub događaj koji obaveštava ostale servise o promeni kvaliteta senzora.

### 2.4. Sensor Management Service
Ovaj servis je odgovoran za upravljanje i konfiguraciju senzora. 

Servis prati status senzora pomoću heartbeat mehanizma. Servis sluša na nove podatke od Ingestion servisa i ažurira status senzora na osnovu toga. Ukoliko senzor ne pošalje podatke u određenom vremenskom periodu, servis označava senzor kao neaktivan i aktivira novi senzor, što znači da postoji pool rezervnih senzora koji se mogu aktivirati kada primarni senzor postane neaktivan.
Kada se aktivira novi senzor, kreira se novi pub-sub događaj koji obaveštava ostale servise o promeni statusa senzora. Senzor se može i ručno aktivirati ili deaktivirati putem API-ja, što takođe kreira odgovarajući pub-sub događaj.

Servis poseduje relacionu bazu podataka koja čuva sve informacije o senzorima, uključujući njihov status, konfiguraciju i istoriju promena. Koristi se PostgreSQL baza podataka, koja omogućava efikasno skladištenje i upite nad podacima o senzorima. 

Za čuvanje heartbeat informacija koristi se in-memory skladište, poput Redis-a, koje omogućava brzo čitanje i pisanje podataka o statusu senzora.


### 2.5. Notification Service
Ovaj servis je odgovoran za slanje obaveštenja korisnicima u realnom vremenu o statusu senzora i sistemu. Servis koristi pub-sub mehanizam za primanje događaja o promenama statusa senzora i kvalitetu senzora, a zatim šalje obaveštenja povezanim UI klijentima putem WebSocket konekcija.

Servis periodično šalje nove vrednosti na senzorima (da bi izbegli prenatrpavanje klijenata) i koristi mehanizam rate limiting-a kako bi se osiguralo da se obaveštenja šalju u kontrolisanim intervalima. U slučajevima alarma, promene stanja senzora ili drugih kritičnih događaja, servis šalje obaveštenja odmah, bez obzira na rate limiting.

### 2.6. Analytics Service
Ovaj servis je odgovoran za analizu prikupljenih podataka i generisanje izveštaja. Servis koristi prikupljene podatke o senzorima i njihovom kvalitetu da bi generisao izveštaje o performansama senzora, trendovima u podacima i drugim relevantnim informacijama. Ovaj servis predstavlja API composition patern koji obuhvata podatke iz Ingestion servisa, Concensus servisa i Sensor Management servisa kako bi kreirao sveobuhvatne izveštaje za korisnike. Servis koristi relacionu bazu podataka, kao što je PostgreSQL, za cache-ovanje rezultata analiza i izveštaja, čime se omogućava brži pristup često traženim informacijama. Iz ovoga proizilazi da ostali servisi nude odgovarajuće API-je za pristup podacima, a Analytics Service koristi te API-je za prikupljanje potrebnih informacija za analizu i generisanje izveštaja.

### 2.7. Auth Service
Ovaj servis je odgovoran za autentifikaciju i autorizaciju klijenata. Servis koristi JSON Web Tokens (JWT) za autentifikaciju korisnika i kontrolu pristupa. Klijenti se autentifikuju putem API Gateway-a, koji prosleđuje zahteve Auth servisu za verifikaciju tokena i autorizaciju pristupa određenim resursima. Servis koristi relacionu bazu podataka, kao što je PostgreSQL, za skladištenje korisničkih informacija, uključujući korisnička imena, lozinke (hashirane) i informacije o dozvolama. 

Osim korisničkih naloga, Auth servis takodje upravlja izdavanjem tokena senzorima, koji se koriste za autentifikaciju senzora prilikom slanja podataka Ingestion servisu. Ovi tokeni imaju ograničen rok trajanja i specifične dozvole koje definišu koje operacije senzor može izvršavati.

Koriste se Access i  Refresh tokeni, uz rotaciju refresh tokena, kako bi se osigurala sigurnost i kontrola pristupa. Access tokeni imaju kratak rok trajanja, dok refresh tokeni imaju duži rok trajanja i mogu se koristiti za dobijanje novih access tokena bez ponovne autentifikacije korisnika.

### 2.8. Komunikacija
Klijenti sa servisima komuniciraju putem REST API-ja, dok servisi međusobno komuniciraju slanjem događaja putem pub-sub sistema i direktnim API pozivima putem gRPC-a.


## 3. Sigurnost i bezbednost
Bezbedna komunikacija izmedju servisa obezbedjuje se korišćenjem TLS enkripcije. Svi servisi su dizajnirani da budu otporni na napade, sa implementiranim mehanizmima za detekciju i prevenciju napada, kao što su rate limiting, autentifikacija i autorizacija.

## 4. Deployment i skalabilnost
Svi servisi su kontejnerizovani koristeći Docker, što omogućava jednostavan deployment i skalabilnost. Servisi se mogu skalirati horizontalno dodavanjem više instanci, a orkestracija se vrši pomoću Kubernetes-a, koji omogućava automatsko skaliranje, balansiranje opterećenja i upravljanje resursima.
