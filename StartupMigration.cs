using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;

namespace SupportDesk
{
    /// <summary>Idempotent startup schema/data migration. Does not replace user databases.</summary>
    internal static class StartupMigration
    {
        private const string Schema = @"CREATE TABLE IF NOT EXISTS ""Users"" (""id"" INTEGER PRIMARY KEY AUTOINCREMENT, ""family"" TEXT DEFAULT '', ""name"" TEXT DEFAULT '', ""father"" TEXT DEFAULT '', ""initials"" TEXT DEFAULT '', ""gender"" TEXT DEFAULT '', ""number"" TEXT DEFAULT '', ""email"" TEXT DEFAULT '', ""depart"" TEXT DEFAULT '', ""position"" TEXT DEFAULT '', ""login"" TEXT DEFAULT '', ""pass"" TEXT DEFAULT '', ""groupUser"" TEXT DEFAULT '', ""image"" TEXT DEFAULT '', ""typeAccess"" TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS ""Requests"" (""id"" INTEGER PRIMARY KEY AUTOINCREMENT, ""theDate"" TEXT DEFAULT '', ""tema"" TEXT DEFAULT '', ""typeProblem"" TEXT DEFAULT '', ""userReq"" TEXT DEFAULT '', ""kabinet"" TEXT DEFAULT '', ""discription"" TEXT DEFAULT '', ""date"" TEXT DEFAULT '', ""time"" TEXT DEFAULT '', ""status"" TEXT DEFAULT '', ""executor"" TEXT DEFAULT '', ""executor2"" TEXT DEFAULT '', ""nameFile"" TEXT DEFAULT '', ""pathFile"" TEXT DEFAULT '', ""nameFileResult"" TEXT DEFAULT '', ""pathFileResult"" TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS ""Cartridges"" (""ID"" INTEGER PRIMARY KEY AUTOINCREMENT, ""Cabinet"" TEXT DEFAULT '', ""FIO"" TEXT DEFAULT '', ""NamePrinter"" TEXT DEFAULT '', ""Cartridge"" TEXT DEFAULT '', ""Date1"" TEXT DEFAULT '', ""Date2"" TEXT DEFAULT '', ""Date3"" TEXT DEFAULT '', ""Date4"" TEXT DEFAULT '', ""Date5"" TEXT DEFAULT '', ""Date6"" TEXT DEFAULT '', ""Date7"" TEXT DEFAULT '', ""Date8"" TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS ""Licenses"" (""ID"" INTEGER PRIMARY KEY AUTOINCREMENT, ""cabinet"" TEXT DEFAULT '', ""software"" TEXT DEFAULT '', ""check"" TEXT DEFAULT '', ""pdf"" TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS ""Inventory"" (""ID"" INTEGER PRIMARY KEY AUTOINCREMENT, ""name"" TEXT DEFAULT '', ""purchaseDate"" TEXT DEFAULT '', ""invNum"" TEXT DEFAULT '', ""amount"" TEXT DEFAULT '', ""location"" TEXT DEFAULT '', ""factLocation"" TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS ""Cabinets"" (""ID"" INTEGER PRIMARY KEY AUTOINCREMENT, ""cabinet"" TEXT DEFAULT '', ""licenses"" TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS ""TypeJob"" (""ID"" INTEGER PRIMARY KEY AUTOINCREMENT, ""typeJob"" TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS ""Departaments"" (""ID"" INTEGER PRIMARY KEY AUTOINCREMENT, ""depart"" TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS ""Positions"" (""ID"" INTEGER PRIMARY KEY AUTOINCREMENT, ""position"" TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS ""Status"" (""ID"" INTEGER PRIMARY KEY AUTOINCREMENT, ""status"" TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS ""Executors"" (""ID"" INTEGER PRIMARY KEY AUTOINCREMENT, ""initials"" TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS ""Passwords"" (""ID"" INTEGER PRIMARY KEY AUTOINCREMENT, ""Site"" TEXT DEFAULT '', ""Login"" TEXT DEFAULT '', ""Pass"" TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS ""Contacts"" (""ID"" INTEGER PRIMARY KEY AUTOINCREMENT, ""Company"" TEXT DEFAULT '', ""Name"" TEXT DEFAULT '', ""Phone"" TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS ""Roles"" (""id"" INTEGER PRIMARY KEY AUTOINCREMENT, ""role"" TEXT DEFAULT '', ""name"" TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS ""RightsRoles"" (""id"" INTEGER PRIMARY KEY AUTOINCREMENT, ""role"" TEXT DEFAULT '', ""name"" TEXT DEFAULT '', ""heightWindow"" TEXT DEFAULT '', ""avatar"" TEXT DEFAULT '', ""w_calendar"" TEXT DEFAULT '', ""w_users"" TEXT DEFAULT '', ""w_inventory"" TEXT DEFAULT '', ""f_typeJob"" TEXT DEFAULT '', ""f_cabinets"" TEXT DEFAULT '', ""f_status"" TEXT DEFAULT '', ""f_executor"" TEXT DEFAULT '', ""mnBtn_closeTicket"" TEXT DEFAULT '', ""mnBtn_createTicket"" TEXT DEFAULT '', ""mnBtn_calendar"" TEXT DEFAULT '', ""mnBtnMenu_about"" TEXT DEFAULT '', ""mnBtnMenu_settings"" TEXT DEFAULT '', ""mnBtnMenu_dataUser"" TEXT DEFAULT '', ""mnBtnMenu_logout"" TEXT DEFAULT '', ""btnMenu_users"" TEXT DEFAULT '', ""btnMenu_cartridges"" TEXT DEFAULT '', ""btnMenu_licenses"" TEXT DEFAULT '', ""btnMenu_inventory"" TEXT DEFAULT '', ""btnMenu_passwords"" TEXT DEFAULT '', ""btnMenu_contacts"" TEXT DEFAULT '', ""btnMenu_database"" TEXT DEFAULT '', ""btnMenu_excel"" TEXT DEFAULT '', ""btnMenu_createTicket"" TEXT DEFAULT '');
CREATE TABLE IF NOT EXISTS ""Functional"" (""id"" INTEGER PRIMARY KEY AUTOINCREMENT, ""role"" TEXT DEFAULT '', ""ticketUser"" TEXT DEFAULT '', ""ticketWorker"" TEXT DEFAULT '', ""ticketAdmin"" TEXT DEFAULT '', ""jobIsTicket"" TEXT DEFAULT '', ""signExecutors"" TEXT DEFAULT '');";
        public static void Apply()
        {
            using (var db = new SQLiteConnection(Database.connectionString))
            {
                db.Open();
                using (var transaction = db.BeginTransaction())
                {
                    // CREATE TABLE IF NOT EXISTS: compatible with existing databases.
                    foreach (var statement in Schema.Split(';'))
                        if (!string.IsNullOrWhiteSpace(statement))
                            Execute(db, transaction, statement);
                    Execute(db, transaction, "CREATE TABLE IF NOT EXISTS __SupportDeskMigrations (key TEXT PRIMARY KEY, appliedUtc TEXT NOT NULL)");
                    SeedReferences(db, transaction);
                    SeedInventory(db, transaction);
                    SeedLicenses(db, transaction);
                    SeedRequests(db, transaction);
                    transaction.Commit();
                }
            }
        }
        private static void Execute(SQLiteConnection db, SQLiteTransaction tx, string sql, params SQLiteParameter[] parameters)
        {
            using (var cmd = new SQLiteCommand(sql, db, tx))
            {
                cmd.Parameters.AddRange(parameters);
                cmd.ExecuteNonQuery();
            }
        }
        private static SQLiteParameter P(string name, object value) => new SQLiteParameter(name, value ?? DBNull.Value);
        private static bool IsDone(SQLiteConnection db, SQLiteTransaction tx, string key)
        {
            using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM __SupportDeskMigrations WHERE key = @key", db, tx))
            { cmd.Parameters.AddWithValue("@key", key); return Convert.ToInt32(cmd.ExecuteScalar()) > 0; }
        }
        private static void Mark(SQLiteConnection db, SQLiteTransaction tx, string key)
        { Execute(db, tx, "INSERT OR IGNORE INTO __SupportDeskMigrations(key, appliedUtc) VALUES(@key,@utc)", P("@key",key),P("@utc",DateTime.UtcNow.ToString("o"))); }
        private static int Count(SQLiteConnection db, SQLiteTransaction tx, string table)
        { using(var cmd=new SQLiteCommand("SELECT COUNT(*) FROM ["+table+"]",db,tx)) return Convert.ToInt32(cmd.ExecuteScalar()); }
        private static bool HasRoom(SQLiteConnection db, SQLiteTransaction tx, string room)
        {
            using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM Cabinets WHERE TRIM(cabinet) = @room", db, tx))
            { cmd.Parameters.AddWithValue("@room", room); return Convert.ToInt32(cmd.ExecuteScalar()) > 0; }
        }
        private static void EnsureRoom(SQLiteConnection db, SQLiteTransaction tx, string room)
        {
            if (!HasRoom(db, tx, room))
                Execute(db, tx, "INSERT INTO Cabinets(cabinet,licenses) VALUES(@r,'')", P("@r", room));
        }
        private static void SeedReferences(SQLiteConnection db, SQLiteTransaction tx)
        {
            // Use names from the single-floor map, never fictitious 101/201 room numbers.
            // The map's BmRoom coordinates and floor remain untouched.
            foreach(var room in KnownRooms) EnsureRoom(db, tx, room);
            foreach(var job in new[]{"Программное обеспечение","Оборудование","Сеть и интернет","Принтеры","Учётные записи"})
                Execute(db,tx,"INSERT INTO TypeJob(typeJob) SELECT @v WHERE NOT EXISTS(SELECT 1 FROM TypeJob WHERE typeJob=@v)",P("@v",job));
            foreach(var status in new[]{"Новая","В работе","Выполнена","Закрыта"})
                Execute(db,tx,"INSERT INTO Status(status) SELECT @v WHERE NOT EXISTS(SELECT 1 FROM Status WHERE status=@v)",P("@v",status));
        }
        private static readonly string[] KnownRooms = new string[] {
            "Актовый зал", "Архив", "Библиотека", "Бухгалтерия", "Вахта", "Инженеры", "Касса", "Киоск", "Общежитие", "Приемная директора", "Серверная", "Спортивный зал", "Столовая", "Техникум", "Улица", "1", "2", "2а", "5", "12", "13", "14", "15", "16", "17", "45", "46", "47", "51", "52", "54", "55", "56", "58", "59", "64", "65", "66", "67", "68", "69", "70", "71", "75", "76", "78", "79", "82", "87", "88", "90", "91", "92", "94", "95", "97", "98", "99", "100"
        };
        private static readonly string[][] Equipment = new string[][] {
            new string[] {"Бухгалтерия","Системный блок Dell OptiPlex"},
                new string[] {"Бухгалтерия","Монитор Philips 24"},
                new string[] {"Бухгалтерия","МФУ HP LaserJet Pro"},
                new string[] {"Касса","Компьютер рабочего места кассира"},
                new string[] {"Касса","Монитор Samsung 24"},
                new string[] {"Библиотека","ПК библиотекаря Lenovo"},
                new string[] {"Библиотека","Сканер документов Canon"},
                new string[] {"Серверная","Сервер файлового хранения"},
                new string[] {"Серверная","ИБП APC Smart-UPS"},
                new string[] {"Серверная","Коммутатор 24 порта"},
                new string[] {"Инженеры","Рабочая станция инженерного отдела"},
                new string[] {"Инженеры","Монитор 27"},
                new string[] {"Вахта","ПК поста охраны"},
                new string[] {"Вахта","Монитор видеонаблюдения"},
                new string[] {"Приемная директора","Ноутбук рабочего места"},
                new string[] {"Приемная директора","Сетевой принтер Brother"},
                new string[] {"Актовый зал","Проектор Epson"},
                new string[] {"Актовый зал","Ноутбук для презентаций"},
                new string[] {"Спортивный зал","ПК административного помещения"},
                new string[] {"Столовая","Точка доступа Wi-Fi"},
                new string[] {"Техникум","Сетевое оборудование"},
                new string[] {"Общежитие","Точка доступа Wi-Fi"},
                new string[] {"15","Системный блок учебного кабинета"},
                new string[] {"47","Лазерный принтер"},
                new string[] {"64","Компьютер учебного кабинета"},
                new string[] {"70","Сетевой принтер"},
                new string[] {"91","Ноутбук преподавателя"},
                new string[] {"58","Компьютер учебного кабинета"},
                new string[] {"12","Компьютер учебного кабинета"},
                new string[] {"51","ПК преподавателя"},
                new string[] {"66","Компьютер учебного кабинета"},
                new string[] {"75","ПК преподавателя"},
                new string[] {"88","Принтер учебного кабинета"},
                new string[] {"97","Компьютер учебного кабинета"},
                new string[] {"52","ПК преподавателя"},
                new string[] {"100","ПК учебного кабинета"}
        };
        private static readonly string[][] Software = new string[][] {
            new string[] {"Бухгалтерия","Microsoft Office LTSC"},
                new string[] {"Бухгалтерия","Windows 11 Pro"},
                new string[] {"Бухгалтерия","Антивирусная защита рабочих станций"},
                new string[] {"Касса","Windows 11 Pro"},
                new string[] {"Касса","Криптографическое ПО"},
                new string[] {"Библиотека","Офисный пакет"},
                new string[] {"Библиотека","Windows 10 Pro"},
                new string[] {"Серверная","Windows Server"},
                new string[] {"Серверная","ПО резервного копирования"},
                new string[] {"Инженеры","САПР (лицензионное ПО)"},
                new string[] {"Инженеры","Windows 11 Pro"},
                new string[] {"Вахта","ПО видеонаблюдения"},
                new string[] {"Приемная директора","Microsoft Office"},
                new string[] {"Приемная директора","Windows 11 Pro"},
                new string[] {"Актовый зал","ПО для презентаций"},
                new string[] {"15","Учебное ПО для лабораторных работ"},
                new string[] {"47","Windows 10 Pro"},
                new string[] {"64","Офисный пакет"},
                new string[] {"70","Драйвер и утилиты принтера"},
                new string[] {"91","Windows 11 Pro"},
                new string[] {"51","Офисный пакет"},
                new string[] {"66","Антивирусная защита"},
                new string[] {"75","Учебное ПО"},
                new string[] {"97","Windows 10 Pro"},
                new string[] {"100","Офисный пакет"}
        };
        private static readonly string[][] Tickets = new string[][] {
            new string[] {"Бухгалтерия","Не печатает сетевой принтер","Принтеры","Очередь печати зависает, документы из бухгалтерской программы не выводятся."},
                new string[] {"52","Нет подключения к интернету","Сеть и интернет","На преподавательском ПК отсутствует доступ к внутренним ресурсам и сайтам."},
                new string[] {"Библиотека","ПК долго загружается","Оборудование","После включения рабочий стол появляется через 8–10 минут."},
                new string[] {"Серверная","Проверить резервное копирование","Оборудование","Необходимо проверить журнал резервного копирования и свободное место на накопителе."},
                new string[] {"Касса","Проблема с электронной подписью","Программное обеспечение","Программа не определяет носитель сертификата при подписании документов."},
                new string[] {"Приемная директора","Нет звука при видеоконференции","Оборудование","Микрофон определяется системой, но звук не передаётся участникам."},
                new string[] {"Инженеры","Не открываются общие сетевые папки","Сеть и интернет","После смены пароля доступ к сетевому каталогу недоступен."},
                new string[] {"Актовый зал","Проектор не выводит изображение","Оборудование","Ноутбук подключён по HDMI, проектор сообщает об отсутствии сигнала."},
                new string[] {"15","Установить учебное ПО","Программное обеспечение","Требуется установка разрешённого учебного программного обеспечения на рабочее место."},
                new string[] {"47","Заменить картридж","Принтеры","На распечатках появились светлые полосы, ресурс тонера подходит к концу."},
                new string[] {"64","Не работает клавиатура","Оборудование","Часть клавиш перестала реагировать после включения компьютера."},
                new string[] {"Бухгалтерия","Ошибки при открытии таблиц","Программное обеспечение","Файлы электронных таблиц открываются с сообщением об ошибке формата."},
                new string[] {"Вахта","Камера видеонаблюдения недоступна","Сеть и интернет","Изображение с входной камеры отсутствует в приложении просмотра."},
                new string[] {"Техникум","Не загружается сайт организации","Сеть и интернет","Уточнить DNS и доступность корпоративного сайта из локальной сети."},
                new string[] {"5","Не включается монитор","Оборудование","Индикатор питания монитора не загорается."},
                new string[] {"70","Не работает сетевой принтер","Принтеры","При печати отображается статус «Недоступен»."},
                new string[] {"Столовая","Слабый сигнал Wi-Fi","Сеть и интернет","Соединение обрывается в зоне кассового рабочего места."},
                new string[] {"Архив","Недостаточно места на диске","Оборудование","Системный диск почти заполнен, обновления не устанавливаются."},
                new string[] {"91","Настроить учётную запись","Учётные записи","После замены оборудования требуется выполнить вход в рабочую учётную запись."},
                new string[] {"Библиотека","Сканер не сохраняет документы","Принтеры","При сканировании в PDF приложение сообщает об ошибке доступа к папке."},
                new string[] {"58","Не запускается браузер","Программное обеспечение","Браузер закрывается сразу после открытия."},
                new string[] {"Инженеры","Коммутатор: проверить порт","Сеть и интернет","У рабочего места периодически пропадает подключение по Ethernet."},
                new string[] {"12","Не работает мышь","Оборудование","Устройство подключено, но курсор не перемещается."},
                new string[] {"Приемная директора","Настроить печать по сети","Принтеры","Новый рабочий компьютер не видит общий принтер."},
                new string[] {"100","Проверка антивирусной защиты","Программное обеспечение","На учебном ПК антивирус сообщает об устаревших базах."},
                new string[] {"Серверная","Проверить состояние ИБП","Оборудование","ИБП периодически подаёт звуковой сигнал при штатном питании."},
                new string[] {"75","Сбросить пароль пользователя","Учётные записи","Сотрудник не может войти в учётную запись после блокировки."},
                new string[] {"Спортивный зал","Нет изображения на экране","Оборудование","Системный блок включается, экран остаётся чёрным."},
                new string[] {"51","Обновить офисный пакет","Программное обеспечение","Требуется установить утверждённую версию офисного пакета."},
                new string[] {"Киоск","Пропадает подключение к сети","Сеть и интернет","Сетевой адаптер периодически отключается."},
                new string[] {"66","Не работают USB-порты","Оборудование","Подключённые флеш-накопители не определяются."},
                new string[] {"Общежитие","Проверить точку доступа","Сеть и интернет","Некоторые устройства не могут подключиться к беспроводной сети."},
                new string[] {"88","Печать с неправильными полями","Принтеры","На документах обрезается нижняя часть текста."},
                new string[] {"Касса","Не открывается рабочая программа","Программное обеспечение","При запуске приложения возникает сообщение об отсутствии подключения."},
                new string[] {"97","Проверить сетевой кабель","Сеть и интернет","Индикатор линка на сетевой карте не светится."},
                new string[] {"Бухгалтерия","Настроить резервное сохранение","Программное обеспечение","Проверить сохранение рабочих документов в утверждённую сетевую папку."}
        };
        private static void SeedInventory(SQLiteConnection db, SQLiteTransaction tx)
        {
            if (IsDone(db,tx,"inventory-realrooms-v3")) return;
            for(int i=0;i<Equipment.Length;i++)
            {
                string room=Equipment[i][0], name=Equipment[i][1], id="SD-"+(10001+i);
                // Only repair the previous migration's explicitly identifiable SD- inventory.
                Execute(db,tx,@"UPDATE Inventory SET name=@n,location=@r,factLocation=@r
                    WHERE invNum=@id AND location IN ('101','102','103','201','202','203','301','302')
                      AND factLocation IN ('101','102','103','201','202','203','301','302')",
                    P("@n",name),P("@r",room),P("@id",id));
                Execute(db,tx,@"INSERT INTO Inventory(name,purchaseDate,invNum,amount,location,factLocation)
                    SELECT @n,@d,@id,'1',@r,@r
                    WHERE NOT EXISTS(SELECT 1 FROM Inventory WHERE invNum=@id)",
                    P("@n",name),P("@d",new DateTime(2022+i%4,1+i%12,5+i%22).ToString("dd.MM.yyyy")),
                    P("@id",id),P("@r",room));
            }
            Mark(db,tx,"inventory-realrooms-v3");
        }
        private static void SeedLicenses(SQLiteConnection db, SQLiteTransaction tx)
        {
            if(IsDone(db,tx,"licenses-realrooms-v3")) return;
            // No license keys or proof-of-purchase documents are invented here.
            for(int i=0;i<Software.Length;i++)
            {
                var room=Software[i][0]; var name=Software[i][1]+" (учебная запись)";
                Execute(db,tx,@"INSERT INTO Licenses(cabinet,software,[check],pdf)
                    SELECT @r,@n,'Не проверено',''
                    WHERE NOT EXISTS(SELECT 1 FROM Licenses WHERE cabinet=@r AND software=@n)",
                    P("@r",room),P("@n",name));
            }
            Mark(db,tx,"licenses-realrooms-v3");
        }
        private static void SeedRequests(SQLiteConnection db, SQLiteTransaction tx)
        {
            if(IsDone(db,tx,"requests-realrooms-v3")) return;
            var workers = new List<string>();
            using(var cmd=new SQLiteCommand(@"SELECT DISTINCT TRIM(initials) FROM Users
                    WHERE UPPER(TRIM(groupUser))='WORKER' AND TRIM(COALESCE(initials,''))<>''",db,tx))
            using(var rd=cmd.ExecuteReader()) while(rd.Read()) workers.Add(rd.GetString(0));
            if(workers.Count==0) return; // Retry on next launch, no demo accounts created.
            foreach(var worker in workers)
                Execute(db,tx,@"INSERT INTO Executors(initials)
                    SELECT @w WHERE NOT EXISTS(SELECT 1 FROM Executors WHERE TRIM(initials)=@w)",P("@w",worker));
            for(int i=0;i<Tickets.Length;i++)
            {
                string marker="[SD-MIGRATION-REQUEST-"+(i+1).ToString("D2")+"]";
                var row=Tickets[i];var room=row[0];var title=row[1];var type=row[2];var desc=marker+" "+row[3];
                var status=i%5==0 ? "Выполнена" : i%3==0 ? "В работе" : "Новая";
                var worker=workers[i%workers.Count];
                var day=DateTime.Today.AddDays(-i%24);
                // Upgrade only the prior migration's tagged records, preserving user-edited statuses/executors.
                Execute(db,tx,@"UPDATE Requests SET tema=@title,typeProblem=@type,kabinet=@room,
                    discription=@description WHERE discription LIKE @prefix
                    AND (kabinet IN ('101','102','103','201','202','203','301','302'))",
                    P("@title",title),P("@type",type),P("@room",room),P("@description",desc),P("@prefix",marker+"%"));
                Execute(db,tx,@"INSERT INTO Requests(theDate,tema,typeProblem,userReq,kabinet,discription,date,time,status,executor,executor2,nameFile,pathFile,nameFileResult,pathFileResult)
                    SELECT @when,@title,@type,'Сотрудник техникума',@room,@description,@date,@time,@status,@worker,'Не назначен','','','',''
                    WHERE NOT EXISTS(SELECT 1 FROM Requests WHERE discription LIKE @prefix)",
                    P("@when",day.ToString("dd.MM.yyyy HH:mm")),P("@title",title),P("@type",type),P("@room",room),
                    P("@description",desc),P("@date",day.ToString("dd.MM.yyyy")),P("@time",day.ToString("dd.MM.yyyy")+"/09:00 - 10:00"),
                    P("@status",status),P("@worker",worker),P("@prefix",marker+"%"));
            }
            Mark(db,tx,"requests-realrooms-v3");
        }
    }
}
