using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SessionManagementApp
{
    class Program
    {
        // Хранилище для имени пользователя, пароля и статуса входа
        static Dictionary<string, (string Password, int Status)> validUsers = new Dictionary<string, (string, int)>();

        // Словарь для хранения открытых сессий
        static Dictionary<string, string> sessions = new Dictionary<string, string>();

        // Блокировки для синхронизации доступа к данным пользователей и сессий
        static readonly object usersLock = new object();
        static readonly object sessionsLock = new object();

        // Флаги для отслеживания изменений в данных пользователей и сессий
        static bool usersDataChanged = false;
        static bool sessionsDataChanged = false;

        static void Main(string[] args)
        {
           
            LoadValidUsers("users.txt");
            InitializeSessionFile("sessions.txt");

            while (true)
            {
                Console.Write("Введите номер сесси. Для завершения сессии введите:'delete <sessionId>' ");
                string input = Console.ReadLine();

                if (input.StartsWith("delete "))// Удаление сессии по указанному sessionId
                {
                    string sessionIdToDelete = input.Substring(7);
                    DeleteSession(sessionIdToDelete);
                }
                else // Обработка нового запроса на сессию
                {
                    HandleSession(input);
                }

                // Сохранение данных в файлы, если они были изменены
                if (usersDataChanged)
                {
                    SaveValidUsers("users.txt");
                    usersDataChanged = false;
                }

                if (sessionsDataChanged)
                {
                    SaveSessions("sessions.txt");
                    sessionsDataChanged = false;
                }
            }
        }

        static void LoadValidUsers(string filename)
        {
            try
            {
                // Чтение и загрузка данных пользователей из файла с блокировкой доступа
                lock (usersLock)
                {
                    validUsers.Clear();
                    foreach (var line in File.ReadAllLines(filename))
                    {
                        var parts = line.Split(',');
                        if (parts.Length == 3)
                        {
                            // Извлекаем данные пользователя: имя, пароль и статус
                            string username = parts[0].Trim();
                            string password = parts[1].Trim();
                            int status = int.Parse(parts[2].Trim());
                            validUsers[username] = (password, status);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка загрузки файла пользователей: " + e.Message);
            }
        }

        static void SaveValidUsers(string filename)
        {
            try
            {
                // Сохранение данных пользователей в файл с блокировкой доступа
                lock (usersLock)
                {
                    var lines = validUsers.Select(kvp => $"{kvp.Key},{kvp.Value.Password},{kvp.Value.Status}");
                    File.WriteAllLines(filename, lines);
                    Console.WriteLine("Данные пользователей сохранены.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка сохранения файла пользователей: " + e.Message);
            }
        }

        static void InitializeSessionFile(string filename)
        {
            // Проверка существования файла сессий (создаем новый, если файл пуст или не существует)
            if (!File.Exists(filename) || new FileInfo(filename).Length == 0)
            {
                File.WriteAllText(filename, string.Empty);
            }
            else
            {
                // Если файл сессий существует, загружаем данные
                LoadSessions(filename);
            }
        }

        static void LoadSessions(string filename)
        {
            try
            {
                // Чтение данных сессий из файла с блокировкой для синхронизации
                lock (sessionsLock)
                {
                    sessions.Clear();
                    foreach (var line in File.ReadAllLines(filename))
                    {
                        var parts = line.Split(',');
                        if (parts.Length == 2)
                        {
                            // Извлекаем данные сессии: sessionId и имя пользователя
                            string sessionId = parts[0].Trim();
                            string username = parts[1].Trim();
                            sessions[sessionId] = username;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка загрузки файла сессий: " + e.Message);
            }
        }

        static void SaveSessions(string filename)
        {
            try
            {
                // Сохранение данных сессий в файл с блокировкой для синхронизации
                lock (sessionsLock)
                {
                    var lines = sessions.Select(kvp => $"{kvp.Key},{kvp.Value}");
                    File.WriteAllLines(filename, lines);
                    Console.WriteLine("Данные сессий сохранены.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Ошибка сохранения файла сессий: " + e.Message);
            }
        }

        static void HandleSession(string sessionId)
        {
            // Обновляем актуальные данные пользователей и сессий
            LoadValidUsers("users.txt");
            LoadSessions("sessions.txt");

            // Проверка на существующую сессию
            if (sessions.ContainsKey(sessionId))
            {
                Console.WriteLine("Вы уже вошли в систему.");
                return;
            }

            Console.Write("Введите логин: ");
            string username = Console.ReadLine();

            if (!validUsers.ContainsKey(username))
            {
                Console.WriteLine("Пользователь не найден.");
                return;
            }

            Console.Write("Введите пароль: ");
            string password = Console.ReadLine();

            if (Authenticate(username, password))
            {
                // Проверка статуса входа (допущен только один вход)
                if (validUsers[username].Status == 1)
                {
                    Console.WriteLine("Этот аккаунт уже используется в другой сессии.");
                }
                else
                {
                    // Создание уникального идентификатора для новой сессии
                    string newSessionId = Guid.NewGuid().ToString();

                    // Обновление статуса входа для пользователя и установка флага изменения данных
                    validUsers[username] = (validUsers[username].Password, 1);
                    usersDataChanged = true;

                    // Сохранение новой сессии
                    sessions[newSessionId] = username;
                    sessionsDataChanged = true;

                    Console.WriteLine($"Успешный вход в систему пользователя {username}. Идентификатор сессии: {newSessionId}");
                }
            }
            else
            {
                Console.WriteLine("Неверный пароль.");
            }
        }

        static bool Authenticate(string username, string password)
        {
            // Сравнение введенного пароля с сохраненным в системе
            return validUsers.ContainsKey(username) && validUsers[username].Password == password;
        }

        static void DeleteSession(string sessionId)
        {
            // Обновляем данные пользователей и сессий перед удалением сессии
            LoadValidUsers("users.txt");
            LoadSessions("sessions.txt");

            if (sessions.TryGetValue(sessionId, out var username))
            {
                // Удаляем сессию и сбрасываем статус входа для пользователя
                sessions.Remove(sessionId);
                validUsers[username] = (validUsers[username].Password, 0);
                usersDataChanged = true;
                sessionsDataChanged = true;

                Console.WriteLine($"Сессия {sessionId} успешно удалена. Пользователь {username} вышел из системы.");
            }
            else
            {
                Console.WriteLine($"Сессия {sessionId} не найдена.");
            }
        }
    }
}
