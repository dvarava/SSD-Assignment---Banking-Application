using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace Banking_Application
{
    public class Data_Access_Layer
    {
        private List<Bank_Account> accounts;
        public static String databaseName = "Banking_Database.db";
        private static Data_Access_Layer instance = new Data_Access_Layer();

        private Data_Access_Layer()
        {
            accounts = new List<Bank_Account>();
        }

        public static Data_Access_Layer getInstance()
        {
            return instance;
        }

        private SqliteConnection getDatabaseConnection()
        {
            String databaseConnectionString = new SqliteConnectionStringBuilder()
            {
                DataSource = Data_Access_Layer.databaseName,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString();

            return new SqliteConnection(databaseConnectionString);
        }

        private void initialiseDatabase()
        {
            using (var connection = getDatabaseConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText =
                @"
                    CREATE TABLE IF NOT EXISTS Bank_Accounts(    
                        accountNo TEXT PRIMARY KEY,
                        name TEXT NOT NULL,
                        address_line_1 TEXT,
                        address_line_2 TEXT,
                        address_line_3 TEXT,
                        town TEXT NOT NULL,
                        balance REAL NOT NULL,
                        accountType INTEGER NOT NULL,
                        overdraftAmount REAL,
                        interestRate REAL
                    ) WITHOUT ROWID
                ";
                command.ExecuteNonQuery();
            }
        }

        public void loadBankAccounts()
        {
            accounts.Clear(); // Ensure list is clean before reload
            if (!File.Exists(Data_Access_Layer.databaseName))
                initialiseDatabase();
            else
            {
                using (var connection = getDatabaseConnection())
                {
                    connection.Open();
                    var command = connection.CreateCommand();
                    command.CommandText = "SELECT * FROM Bank_Accounts";
                    using (SqliteDataReader dr = command.ExecuteReader())
                    {
                        while (dr.Read())
                        {
                            int accountType = dr.GetInt16(7);
                            Bank_Account ba;

                            if (accountType == Account_Type.Current_Account)
                            {
                                Current_Account ca = new Current_Account();
                                ca.overdraftAmount = dr.IsDBNull(8) ? 0 : dr.GetDouble(8);
                                ba = ca;
                            }
                            else
                            {
                                Savings_Account sa = new Savings_Account();
                                sa.interestRate = dr.IsDBNull(9) ? 0 : dr.GetDouble(9);
                                ba = sa;
                            }

                            ba.accountNo = dr.GetString(0);

                            // ALE: Decrypt PII data read from DB
                            ba.name = SecurityHelper.Decrypt(dr.GetString(1));
                            ba.address_line_1 = SecurityHelper.Decrypt(dr.GetString(2));
                            ba.address_line_2 = SecurityHelper.Decrypt(dr.GetString(3));
                            ba.address_line_3 = SecurityHelper.Decrypt(dr.GetString(4));
                            ba.town = SecurityHelper.Decrypt(dr.GetString(5));

                            ba.balance = dr.GetDouble(6);
                            accounts.Add(ba);
                        }
                    }
                }
            }
        }

        public String addBankAccount(Bank_Account ba)
        {
            accounts.Add(ba);

            using (var connection = getDatabaseConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();

                // Encrypt PII data before saving
                command.CommandText = @"
                    INSERT INTO Bank_Accounts (accountNo, name, address_line_1, address_line_2, address_line_3, town, balance, accountType, overdraftAmount, interestRate)
                    VALUES ($accNo, $name, $addr1, $addr2, $addr3, $town, $bal, $type, $overdraft, $interest)";

                command.Parameters.AddWithValue("$accNo", ba.accountNo);
                command.Parameters.AddWithValue("$name", SecurityHelper.Encrypt(ba.name)); // Encrypt
                command.Parameters.AddWithValue("$addr1", SecurityHelper.Encrypt(ba.address_line_1)); // Encrypt
                command.Parameters.AddWithValue("$addr2", SecurityHelper.Encrypt(ba.address_line_2)); // Encrypt
                command.Parameters.AddWithValue("$addr3", SecurityHelper.Encrypt(ba.address_line_3)); // Encrypt
                command.Parameters.AddWithValue("$town", SecurityHelper.Encrypt(ba.town)); // Encrypt
                command.Parameters.AddWithValue("$bal", ba.balance);

                int type = (ba is Current_Account) ? 1 : 2;
                command.Parameters.AddWithValue("$type", type);

                if (ba is Current_Account ca)
                {
                    command.Parameters.AddWithValue("$overdraft", ca.overdraftAmount);
                    command.Parameters.AddWithValue("$interest", DBNull.Value);
                }
                else
                {
                    Savings_Account sa = (Savings_Account)ba;
                    command.Parameters.AddWithValue("$overdraft", DBNull.Value);
                    command.Parameters.AddWithValue("$interest", sa.interestRate);
                }

                command.ExecuteNonQuery();
            }
            return ba.accountNo;
        }

        public Bank_Account findBankAccountByAccNo(String accNo)
        {
            foreach (Bank_Account ba in accounts)
            {
                if (ba.accountNo.Equals(accNo)) return ba;
            }
            return null;
        }

        public bool closeBankAccount(String accNo)
        {
            Bank_Account toRemove = findBankAccountByAccNo(accNo);

            if (toRemove == null) return false;

            accounts.Remove(toRemove);

            using (var connection = getDatabaseConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();

                command.CommandText = "DELETE FROM Bank_Accounts WHERE accountNo = $accNo";
                command.Parameters.AddWithValue("$accNo", toRemove.accountNo);
                command.ExecuteNonQuery();
            }
            return true;
        }

        public bool lodge(String accNo, double amountToLodge)
        {
            Bank_Account toLodgeTo = findBankAccountByAccNo(accNo);

            if (toLodgeTo == null) return false;

            toLodgeTo.lodge(amountToLodge);

            using (var connection = getDatabaseConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "UPDATE Bank_Accounts SET balance = $bal WHERE accountNo = $accNo";
                command.Parameters.AddWithValue("$bal", toLodgeTo.balance);
                command.Parameters.AddWithValue("$accNo", toLodgeTo.accountNo);
                command.ExecuteNonQuery();
            }
            return true;
        }

        public bool withdraw(String accNo, double amountToWithdraw)
        {
            Bank_Account toWithdrawFrom = findBankAccountByAccNo(accNo);

            if (toWithdrawFrom == null) return false;

            bool result = toWithdrawFrom.withdraw(amountToWithdraw);

            if (!result) return false;

            using (var connection = getDatabaseConnection())
            {
                connection.Open();
                var command = connection.CreateCommand();
                command.CommandText = "UPDATE Bank_Accounts SET balance = $bal WHERE accountNo = $accNo";
                command.Parameters.AddWithValue("$bal", toWithdrawFrom.balance);
                command.Parameters.AddWithValue("$accNo", toWithdrawFrom.accountNo);
                command.ExecuteNonQuery();
            }
            return true;
        }
    }
}