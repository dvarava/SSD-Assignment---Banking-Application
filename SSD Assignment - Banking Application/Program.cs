using System;
using System.DirectoryServices.AccountManagement;

namespace Banking_Application
{
    public class Program
    {
        // Session variable to store the currently logged in user
        private static UserPrincipal currentUser = null;

        public static void Main(string[] args)
        {
            if (!AuthenticateUser())
            {
                Console.WriteLine("Access Denied. Exiting...");
                Console.ReadKey();
                return;
            }

            Data_Access_Layer dal = Data_Access_Layer.getInstance();
            dal.loadBankAccounts();
            bool running = true;

            do
            {
                Console.WriteLine("");
                Console.WriteLine("***Banking Application Menu***");
                Console.WriteLine($"Logged in as: {currentUser.DisplayName}");
                Console.WriteLine("1. Add Bank Account");
                Console.WriteLine("2. Close Bank Account (Admin Only)");
                Console.WriteLine("3. View Account Information");
                Console.WriteLine("4. Make Lodgement");
                Console.WriteLine("5. Make Withdrawal");
                Console.WriteLine("6. Exit");
                Console.WriteLine("CHOOSE OPTION:");
                String option = Console.ReadLine();

                try
                {
                    switch (option)
                    {
                        case "1": // Add
                            HandleAddAccount(dal);
                            break;
                        case "2": // Close
                            HandleCloseAccount(dal);
                            break;
                        case "3": // View
                            HandleViewAccount(dal);
                            break;
                        case "4": // Lodge
                            HandleTransaction(dal, "Lodge");
                            break;
                        case "5": // Withdraw
                            HandleTransaction(dal, "Withdraw");
                            break;
                        case "6":
                            running = false;
                            break;
                        default:
                            Console.WriteLine("INVALID OPTION CHOSEN");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"An Error Occurred: {ex.Message}");
                    Logger.LogSecurityEvent($"Application Error: {ex.Message}");
                }

            } while (running != false);
        }

        private static bool AuthenticateUser()
        {
            Console.WriteLine("--- AUTHENTICATION REQUIRED ---");
            Console.Write("Username: ");
            string username = Console.ReadLine();
            Console.Write("Password: ");

            string password = "";
            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(true);
                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter)
                {
                    password += key.KeyChar;
                    Console.Write("*");
                }
                else if (key.Key == ConsoleKey.Backspace && password.Length > 0)
                {
                    password = password.Substring(0, (password.Length - 1));
                    Console.Write("\b \b");
                }
            } while (key.Key != ConsoleKey.Enter);
            Console.WriteLine("");

            try
            {
                // Connect to AD on ITSLIGO.LAN
                using (PrincipalContext pc = new PrincipalContext(ContextType.Domain, "ITSLIGO.LAN"))
                {
                    if (pc.ValidateCredentials(username, password))
                    {
                        currentUser = UserPrincipal.FindByIdentity(pc, username);

                        // Check if user is in Bank Teller group
                        GroupPrincipal group = GroupPrincipal.FindByIdentity(pc, "Bank Teller");
                        if (currentUser.IsMemberOf(group))
                        {
                            Logger.LogSecurityEvent($"Successful Login: {username}");
                            return true;
                        }
                        else
                        {
                            Console.WriteLine("User is not authorized (Not in Bank Teller Group).");
                            Logger.LogSecurityEvent($"Unauthorized Access Attempt (Bad Group): {username}");
                            return false;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Invalid Credentials.");
                        Logger.LogSecurityEvent($"Failed Login Attempt: {username}");
                        return false;
                    }
                }
            }
            catch (PrincipalServerDownException)
            {
                Console.WriteLine("ERROR: Cannot contact Active Directory Server.");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine("Auth Error: " + ex.Message);
                return false;
            }
        }

        private static bool IsAdmin()
        {
            try
            {
                using (PrincipalContext pc = new PrincipalContext(ContextType.Domain, "ITSLIGO.LAN"))
                {
                    GroupPrincipal adminGroup = GroupPrincipal.FindByIdentity(pc, "Bank Teller Administrator");
                    if (currentUser.IsMemberOf(adminGroup)) return true;
                }
            }
            catch
            {
                return false;
            }
            return false;
        }

        private static void HandleAddAccount(Data_Access_Layer dal)
        {
            // In the original code, gathering inputs takes many lines
            // I implemented a shortened version here for the solution

            Console.WriteLine("Enter Name:"); string name = Console.ReadLine();
            Console.WriteLine("Address 1:"); string ad1 = Console.ReadLine();
            Console.WriteLine("Address 2:"); string ad2 = Console.ReadLine();
            Console.WriteLine("Address 3:"); string ad3 = Console.ReadLine();
            Console.WriteLine("Town:"); string town = Console.ReadLine();
            Console.WriteLine("Balance:"); double bal = Convert.ToDouble(Console.ReadLine());

            // 1=Current, 2=Savings
            Console.WriteLine("Type (1=Curr, 2=Sav):"); string type = Console.ReadLine();

            Bank_Account ba;
            if (type == "1")
            {
                Console.WriteLine("Overdraft:"); double od = Convert.ToDouble(Console.ReadLine());
                ba = new Current_Account(name, ad1, ad2, ad3, town, bal, od);
            }
            else
            {
                Console.WriteLine("Interest:"); double interest = Convert.ToDouble(Console.ReadLine());
                ba = new Savings_Account(name, ad1, ad2, ad3, town, bal, interest);
            }

            string accNo = dal.addBankAccount(ba);
            Console.WriteLine($"Account Created: {accNo}");

            // LOGGING
            Logger.LogTransaction(currentUser.DisplayName, accNo, name, "Account Creation");
        }

        private static void HandleCloseAccount(Data_Access_Layer dal)
        {
            // ACCESS CONTROL CHECK
            if (!IsAdmin())
            {
                Console.WriteLine("Authorization Failed: Administrator Approval Required for Account Deletion.");
                Logger.LogSecurityEvent($"Unauthorized Deletion Attempt by {currentUser.DisplayName}");
                return;
            }

            Console.WriteLine("Enter Account Number:");
            string accNo = Console.ReadLine();
            Bank_Account ba = dal.findBankAccountByAccNo(accNo);

            if (ba != null)
            {
                dal.closeBankAccount(accNo);
                Console.WriteLine("Account Closed.");
                // LOGGING
                Logger.LogTransaction(currentUser.DisplayName, accNo, ba.name, "Account Closure");
            }
            else
            {
                Console.WriteLine("Account Not Found.");
            }
        }

        private static void HandleViewAccount(Data_Access_Layer dal)
        {
            Console.WriteLine("Enter Account Number:");
            string accNo = Console.ReadLine();
            Bank_Account ba = dal.findBankAccountByAccNo(accNo);
            if (ba != null)
            {
                Console.WriteLine(ba.ToString());
                Logger.LogTransaction(currentUser.DisplayName, accNo, ba.name, "Balance Query");
            }
        }

        private static void HandleTransaction(Data_Access_Layer dal, string type)
        {
            Console.WriteLine("Enter Account Number:");
            string accNo = Console.ReadLine();
            Bank_Account ba = dal.findBankAccountByAccNo(accNo);

            if (ba == null)
            {
                Console.WriteLine("Account Not Found");
                return;
            }

            Console.WriteLine("Enter Amount:");
            double amount = Convert.ToDouble(Console.ReadLine());

            // OPTIONAL REASON for > 10,000
            string reason = "N/A";
            if (amount > 10000)
            {
                Console.WriteLine("Amount exceeds €10,000. Please enter reason for transaction:");
                reason = Console.ReadLine();
            }

            bool success = false;
            if (type == "Lodge") success = dal.lodge(accNo, amount);
            else success = dal.withdraw(accNo, amount);

            if (success)
            {
                Console.WriteLine("Transaction Successful.");
                Logger.LogTransaction(currentUser.DisplayName, accNo, ba.name, type, reason);
            }
            else
            {
                Console.WriteLine("Transaction Failed (Insufficient Funds or Error).");
            }
        }
    }
}