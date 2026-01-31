using Microsoft.Data.Sqlite;
using MyNetBoot.Shared.Models;

namespace MyNetBoot.Server.Services;

/// <summary>
/// Foydalanuvchilarni boshqarish xizmati (SQLite database)
/// </summary>
public class UserService
{
    private readonly string _connectionString;

    public UserService(string dataPath)
    {
        var dbPath = Path.Combine(dataPath, "config", "users.db");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        _connectionString = $"Data Source={dbPath}";
        
        InitializeDatabase();
    }

    private void InitializeDatabase()
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        // Users jadvali
        var createUsersTable = @"
            CREATE TABLE IF NOT EXISTS Users (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Familya TEXT NOT NULL CHECK(length(Familya) >= 5),
                Ism TEXT NOT NULL CHECK(length(Ism) >= 3),
                TelefonRaqam TEXT NOT NULL UNIQUE CHECK(length(TelefonRaqam) = 9),
                Parol TEXT NOT NULL CHECK(length(Parol) >= 3),
                Balans REAL NOT NULL DEFAULT 0 CHECK(Balans >= 0),
                Holat TEXT NOT NULL DEFAULT 'block' CHECK(Holat IN ('block', 'faol'))
            );
        ";
        using var cmd1 = new SqliteCommand(createUsersTable, connection);
        cmd1.ExecuteNonQuery();

        // Seanslar jadvali (tarix)
        var createSeansTable = @"
            CREATE TABLE IF NOT EXISTS Seanslar (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                UserId INTEGER NOT NULL,
                Familya TEXT NOT NULL,
                Ism TEXT NOT NULL,
                TelefonRaqam TEXT NOT NULL,
                YechilganBalans REAL NOT NULL,
                BoshlashVaqti TEXT NOT NULL,
                TugashVaqti TEXT NOT NULL,
                OynalganMinut INTEGER NOT NULL,
                FOREIGN KEY (UserId) REFERENCES Users(Id)
            );
        ";
        using var cmd2 = new SqliteCommand(createSeansTable, connection);
        cmd2.ExecuteNonQuery();

        // Test foydalanuvchilarni qo'shish (agar mavjud bo'lmasa)
        InsertTestUsers(connection);
    }

    private void InsertTestUsers(SqliteConnection connection)
    {
        var checkSql = "SELECT COUNT(*) FROM Users";
        using var checkCmd = new SqliteCommand(checkSql, connection);
        var count = Convert.ToInt32(checkCmd.ExecuteScalar());
        
        if (count == 0)
        {
            var testUsers = new[]
            {
                ("Karimov", "Javlon", "901234567", "abc123", 5000m, "faol"),
                ("Saidova", "Malika", "902345678", "xyz789", 3000m, "faol"),
                ("Rahimov", "Sardor", "903456789", "qwe456", 1500m, "block")
            };

            foreach (var (familya, ism, telefon, parol, balans, holat) in testUsers)
            {
                var sql = @"
                    INSERT INTO Users (Familya, Ism, TelefonRaqam, Parol, Balans, Holat)
                    VALUES (@Familya, @Ism, @TelefonRaqam, @Parol, @Balans, @Holat)
                ";
                using var cmd = new SqliteCommand(sql, connection);
                cmd.Parameters.AddWithValue("@Familya", familya);
                cmd.Parameters.AddWithValue("@Ism", ism);
                cmd.Parameters.AddWithValue("@TelefonRaqam", telefon);
                cmd.Parameters.AddWithValue("@Parol", parol);
                cmd.Parameters.AddWithValue("@Balans", balans);
                cmd.Parameters.AddWithValue("@Holat", holat);
                cmd.ExecuteNonQuery();
            }
            
            Console.WriteLine("[DB] 3 ta test foydalanuvchi qo'shildi.");
        }
    }

    /// <summary>
    /// Telefon raqam va parol bilan kirish
    /// </summary>
    public User? Login(string telefonRaqam, string parol)
    {
        // Telefon raqamdan faqat raqamlarni olish
        var cleanPhone = new string(telefonRaqam.Where(char.IsDigit).ToArray());
        
        // Parolni tozalash (boshidagi va oxiridagi probellarni olib tashlash)
        var cleanPassword = parol.Trim();

        if (cleanPhone.Length != 9)
            return null;

        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var sql = "SELECT * FROM Users WHERE TelefonRaqam = @Telefon AND Parol = @Parol";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Telefon", cleanPhone);
        cmd.Parameters.AddWithValue("@Parol", cleanPassword);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new User
            {
                Id = reader.GetInt32(0),
                Familya = reader.GetString(1),
                Ism = reader.GetString(2),
                TelefonRaqam = reader.GetString(3),
                Parol = reader.GetString(4),
                Balans = reader.GetDecimal(5),
                Holat = reader.GetString(6) == "faol" ? UserHolat.Faol : UserHolat.Block
            };
        }
        return null;
    }

    /// <summary>
    /// Foydalanuvchi balansini yangilash
    /// </summary>
    public bool UpdateBalans(int userId, decimal yangiBalans)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var sql = "UPDATE Users SET Balans = @Balans WHERE Id = @Id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Balans", Math.Max(0, yangiBalans));
        cmd.Parameters.AddWithValue("@Id", userId);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Foydalanuvchi holatini yangilash
    /// </summary>
    public bool UpdateHolat(int userId, UserHolat holat)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var sql = "UPDATE Users SET Holat = @Holat WHERE Id = @Id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Holat", holat == UserHolat.Faol ? "faol" : "block");
        cmd.Parameters.AddWithValue("@Id", userId);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Seansni saqlash
    /// </summary>
    public bool SaveSeans(UserSeans seans)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var sql = @"
            INSERT INTO Seanslar (UserId, Familya, Ism, TelefonRaqam, YechilganBalans, BoshlashVaqti, TugashVaqti, OynalganMinut)
            VALUES (@UserId, @Familya, @Ism, @TelefonRaqam, @YechilganBalans, @BoshlashVaqti, @TugashVaqti, @OynalganMinut)
        ";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@UserId", seans.UserId);
        cmd.Parameters.AddWithValue("@Familya", seans.Familya);
        cmd.Parameters.AddWithValue("@Ism", seans.Ism);
        cmd.Parameters.AddWithValue("@TelefonRaqam", seans.TelefonRaqam);
        cmd.Parameters.AddWithValue("@YechilganBalans", seans.YechilganBalans);
        cmd.Parameters.AddWithValue("@BoshlashVaqti", seans.BoshlashVaqti.ToString("O"));
        cmd.Parameters.AddWithValue("@TugashVaqti", seans.TugashVaqti.ToString("O"));
        cmd.Parameters.AddWithValue("@OynalganMinut", seans.OynalganMinut);
        return cmd.ExecuteNonQuery() > 0;
    }

    /// <summary>
    /// Foydalanuvchini ID bo'yicha olish
    /// </summary>
    public User? GetById(int userId)
    {
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var sql = "SELECT * FROM Users WHERE Id = @Id";
        using var cmd = new SqliteCommand(sql, connection);
        cmd.Parameters.AddWithValue("@Id", userId);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            return new User
            {
                Id = reader.GetInt32(0),
                Familya = reader.GetString(1),
                Ism = reader.GetString(2),
                TelefonRaqam = reader.GetString(3),
                Parol = reader.GetString(4),
                Balans = reader.GetDecimal(5),
                Holat = reader.GetString(6) == "faol" ? UserHolat.Faol : UserHolat.Block
            };
        }
        return null;
    }

    /// <summary>
    /// Barcha foydalanuvchilarni olish
    /// </summary>
    public List<User> GetAllUsers()
    {
        var users = new List<User>();
        
        using var connection = new SqliteConnection(_connectionString);
        connection.Open();

        var sql = "SELECT * FROM Users ORDER BY Id";
        using var cmd = new SqliteCommand(sql, connection);
        using var reader = cmd.ExecuteReader();
        
        while (reader.Read())
        {
            users.Add(new User
            {
                Id = reader.GetInt32(0),
                Familya = reader.GetString(1),
                Ism = reader.GetString(2),
                TelefonRaqam = reader.GetString(3),
                Parol = reader.GetString(4),
                Balans = reader.GetDecimal(5),
                Holat = reader.GetString(6) == "faol" ? UserHolat.Faol : UserHolat.Block
            });
        }
        
        return users;
    }
}
