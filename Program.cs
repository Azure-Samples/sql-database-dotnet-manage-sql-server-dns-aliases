// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.Samples.Common;
using Azure.ResourceManager.Resources;
using Azure.ResourceManager;
using Azure.ResourceManager.Sql;
using Azure.ResourceManager.Sql.Models;
using System;
using System.Data.SqlClient;

namespace ManageSqlServerDnsAliases
{
    public class Program
    {
        private static ResourceIdentifier? _resourceGroupId = null;
        private static readonly string dbName = "dbSample";

        /**
         * Azure SQL sample for managing SQL Server DNS Aliases.
         *  - Create two SQL Servers "test" and "production", each with an empty database.
         *  - Create a new table and insert some expected values into each database.
         *  - Create a SQL Server DNS Alias to the "test" SQL database.
         *  - Query the "test" SQL database via the DNS alias and print the result.
         *  - Use the SQL Server DNS alias to acquire the "production" SQL database.
         *  - Query the "production" SQL database via the DNS alias and print the result.
         *  - Delete the SQL Servers
         */
        public static async Task RunSample(ArmClient client)
        {
            try
            {
                // ============================================================
                //Get default subscription
                SubscriptionResource subscription = await client.GetDefaultSubscriptionAsync();

                //Create a resource group in the EastUS region
                string rgName = Utilities.CreateRandomName("rgSQLServer");
                Utilities.Log("Creating resource group...");
                var rgLro = await subscription.GetResourceGroups().CreateOrUpdateAsync(WaitUntil.Completed, rgName, new ResourceGroupData(AzureLocation.EastUS));
                ResourceGroupResource resourceGroup = rgLro.Value;
                _resourceGroupId = resourceGroup.Id;
                Utilities.Log($"Created a resource group with name: {resourceGroup.Data.Name} ");

                // ============================================================
                // Create a "test" SQL Server.
                Utilities.Log("Creating a SQL server for test related activities...");

                var sqlServerForTestName = Utilities.CreateRandomName("sqltest");
                string sqlAdmin = "sqladmin1234";
                string sqlAdminPwd = Utilities.CreatePassword();
                var sqlServerForTestData = new SqlServerData(AzureLocation.EastUS)
                {
                    AdministratorLogin = sqlAdmin,
                    AdministratorLoginPassword = sqlAdminPwd
                };
                var sqlServerForTest = (await resourceGroup.GetSqlServers().CreateOrUpdateAsync(WaitUntil.Completed, sqlServerForTestName, sqlServerForTestData)).Value;
                Utilities.Log($"Created a SQL Server with name: {sqlServerForTest.Data.Name}");

                Utilities.Log("Creating a range ipaddress firewall rule...");
                string testFirewallRuleName = Utilities.CreateRandomName("allowAll");
                var testFirewallRuleData = new SqlFirewallRuleData()
                {
                    StartIPAddress = "0.0.0.1",
                    EndIPAddress = "255.255.255.255"
                };
                var testFirewallRule = (await sqlServerForTest.GetSqlFirewallRules().CreateOrUpdateAsync(WaitUntil.Completed, testFirewallRuleName, testFirewallRuleData)).Value;
                Utilities.Log($"Created a range ipaddress firewall rule with name: {testFirewallRule.Data.Name}");

                Utilities.Log("Creating a database on SQL Server...");
                SqlDatabaseData testDBData = new SqlDatabaseData(AzureLocation.EastUS)
                {
                    Sku = new SqlSku("Basic")
                };
                var testDB = (await sqlServerForTest.GetSqlDatabases().CreateOrUpdateAsync(WaitUntil.Completed, dbName, testDBData)).Value;
                Utilities.Log($"Created a database with name: {testDB.Data.Name}");

                // ============================================================
                // Create a connection to the "test" SQL Server.
                Utilities.Log("Creating a connection to the \"test\" SQL Server");
                var connectionToSqlTestUrl = $"user id={sqlAdmin};" +
                                       $"password={sqlAdminPwd};" +
                                       $"server={sqlServerForTest.Data.FullyQualifiedDomainName};" +
                                       $"database={testDB.Data.Name}; " +
                                       "Trusted_Connection=False;" +
                                       "Encrypt=True;" +
                                       "connection timeout=30";

                // Create a connection to the SQL Server.
                using (SqlConnection conTest = new SqlConnection(connectionToSqlTestUrl))
                {
                    conTest.Open();

                    // ============================================================
                    // Create a new table into the "test" SQL Server database and insert one value.
                    Utilities.Log("Creating a new table into the \"test\" SQL Server database and insert one value");

                    string sqlCreateTableCommand = "CREATE TABLE [Dns_Alias_Sample_Test] ([Name] [varchar](30) NOT NULL)";
                    SqlCommand createTable = new SqlCommand(sqlCreateTableCommand, conTest);
                    createTable.ExecuteNonQuery();
                    string sqlInsertCommand = "INSERT INTO Dns_Alias_Sample_Test VALUES ('Test')";
                    SqlCommand insertValue = new SqlCommand(sqlInsertCommand, conTest);
                    insertValue.ExecuteNonQuery();

                    // Close the connection to the "test" database
                    conTest.Close();
                }

                // ============================================================
                // Create a "production" SQL Server.
                Utilities.Log("Creating a SQL server for production related activities");

                var sqlServerForProdName = Utilities.CreateRandomName("sqlprod");
                var sqlServerForProdData = new SqlServerData(AzureLocation.SouthCentralUS)
                {
                    AdministratorLogin = sqlAdmin,
                    AdministratorLoginPassword = sqlAdminPwd
                };
                var sqlServerForProd = (await resourceGroup.GetSqlServers().CreateOrUpdateAsync(WaitUntil.Completed, sqlServerForProdName, sqlServerForProdData)).Value;
                Utilities.Log($"Created a SQL Server with name: {sqlServerForProd.Data.Name}");

                Utilities.Log("Creating a range ipaddress firewall rule...");
                string prodFirewallRuleName = Utilities.CreateRandomName("allowAll");
                var prodFirewallRuleData = new SqlFirewallRuleData()
                {
                    StartIPAddress = "0.0.0.1",
                    EndIPAddress = "255.255.255.255"
                };
                var prodFirewallRule = (await sqlServerForProd.GetSqlFirewallRules().CreateOrUpdateAsync(WaitUntil.Completed, prodFirewallRuleName, prodFirewallRuleData)).Value;
                Utilities.Log($"Created a range ipaddress firewall rule with name: {prodFirewallRule.Data.Name}");

                Utilities.Log("Creating a database on SQL Server...");
                SqlDatabaseData prodDBData = new SqlDatabaseData(AzureLocation.SouthCentralUS)
                {
                    Sku = new SqlSku("Basic")
                };
                var prodDB = (await sqlServerForProd.GetSqlDatabases().CreateOrUpdateAsync(WaitUntil.Completed, dbName, prodDBData)).Value;
                Utilities.Log($"Created a database with name: {prodDB.Data.Name}");

                // ============================================================
                // Create a connection to the "production" SQL Server.

                Utilities.Log("Creating a connection to the \"production\" SQL Server...");

                var connectionToSqlProdUrl = $"user id={sqlAdmin};" +
                                       $"password={sqlAdminPwd};" +
                                       $"server={sqlServerForProd.Data.FullyQualifiedDomainName};" +
                                       $"database={prodDB.Data.Name}; " +
                                       "Trusted_Connection=False;" +
                                       "Encrypt=True;" +
                                       "connection timeout=30";
                // Create a connection to the SQL Server.
                using (SqlConnection conProd = new SqlConnection(connectionToSqlProdUrl))
                {
                    conProd.Open();

                    // ============================================================
                    // Create a new table into the "production" SQL Server database and insert one value.
                    Utilities.Log("Creating a new table into the \"production\" SQL Server database and insert one value...");

                    string sqlCreateTableCommand = "CREATE TABLE [Dns_Alias_Sample_Prod] ([Name] [varchar](30) NOT NULL)";
                    SqlCommand createTable = new SqlCommand(sqlCreateTableCommand, conProd);
                    createTable.ExecuteNonQuery();
                    string sqlInsertCommand = "INSERT INTO Dns_Alias_Sample_Prod VALUES ('Production')";
                    SqlCommand insertValue = new SqlCommand(sqlInsertCommand, conProd);
                    insertValue.ExecuteNonQuery();

                    // Close the connection to the "prod" database
                    conProd.Close();
                }

                // ============================================================
                // Create a SQL Server DNS alias and use it to query the "test" database.
                Utilities.Log("Creating a SQL Server DNS alias and use it to query the \"test\" database...");

                string sqlServerDnsAliasName = Utilities.CreateRandomName("sqlserverdns");
                var sqlServerDnsAlias = (await sqlServerForTest.GetSqlServerDnsAliases().CreateOrUpdateAsync(WaitUntil.Completed, sqlServerDnsAliasName)).Value;
                Utilities.Log("Waiting 3 minutes to delyment...");
                Thread.Sleep(TimeSpan.FromMinutes(3));
                Utilities.Log($"Created a SQL Server DNS alias with name: {sqlServerDnsAlias.Data.Name}");

                var connectionUrl = $"user id={sqlAdmin};" +
                                       $"password={sqlAdminPwd};" +
                                       $"server={sqlServerDnsAlias.Data.AzureDnsRecord};" +
                                       $"database={dbName}; " +
                                       "Trusted_Connection=False;" +
                                       "Encrypt=True;" +
                                       "connection timeout=30";

                // Create a connection to the SQL DNS alias.
                using (SqlConnection conDnsAlias = new SqlConnection(connectionUrl))
                {
                    conDnsAlias.Open();

                    // ============================================================
                    // Create a SQL Server DNS alias and use it to query the "test" database.
                    Utilities.Log("Creating a SQL Server DNS alias and use it to query the \"test\" database...");

                    string sqlCommand = "SELECT * FROM Dns_Alias_Sample_Test;";
                    SqlCommand selectCommand = new SqlCommand(sqlCommand, conDnsAlias);
                    var myReader = selectCommand.ExecuteReader();
                    while (myReader.Read())
                    {
                        Utilities.Log("Query \"test\" database with result: "+myReader["Name"].ToString());
                    }
                    conDnsAlias.Close();
                }

                // ============================================================
                // Use the "production" SQL Server to acquire the SQL Server DNS Alias and use it to query the "production" database.
                Utilities.Log("Using the \"production\" SQL Server to acquire the SQL Server DNS Alias and use it to query the \"production\" database...");

                await sqlServerDnsAlias.DeleteAsync(WaitUntil.Completed);
                sqlServerDnsAlias = (await sqlServerForProd.GetSqlServerDnsAliases().CreateOrUpdateAsync(WaitUntil.Completed, sqlServerDnsAliasName)).Value;
                connectionUrl = $"user id={sqlAdmin};" +
                       $"password={sqlAdminPwd};" +
                       $"server={sqlServerDnsAlias.Data.AzureDnsRecord};" +
                       $"database={dbName}; " +
                       "Trusted_Connection=False;" +
                       "Encrypt=True;" +
                       "connection timeout=30";

                // It takes some time for the DNS alias to reflect the new Server connection
                Utilities.Log("Waiting 10 minutes to connect...");
                Thread.Sleep(TimeSpan.FromMinutes(10));

                // Re-establish the connection.
                Utilities.Log("Re-establish the connection");
                using (SqlConnection conDnsAlias = new SqlConnection(connectionUrl))
                {
                    conDnsAlias.Open();

                    // ============================================================
                    // Create a SQL Server DNS alias and use it to query the "production" database.
                    Utilities.Log("Creating a SQL Server DNS alias and use it to query the \"production\" database");

                    string sqlCommand = "SELECT * FROM Dns_Alias_Sample_Prod;";
                    SqlCommand selectCommand = new SqlCommand(sqlCommand, conDnsAlias);
                    var myReader = selectCommand.ExecuteReader();
                    while (myReader.Read())
                    {
                        Utilities.Log("Query \"production\" database with result: " + myReader["Name"].ToString());
                    }
                    conDnsAlias.Close();
                }

                // Delete the SQL Servers.
                Utilities.Log("Deleting the Sql Servers");
                await sqlServerForTest.DeleteAsync(WaitUntil.Completed);
                await sqlServerForProd.DeleteAsync(WaitUntil.Completed);
            }
            finally
            {
                try
                {
                    if (_resourceGroupId is not null)
                    {
                        Utilities.Log($"Deleting Resource Group...");
                        await client.GetResourceGroupResource(_resourceGroupId).DeleteAsync(WaitUntil.Completed);
                        Utilities.Log($"Deleted Resource Group: {_resourceGroupId.Name}");
                    }
                }
                catch (Exception e)
                {
                    Utilities.Log(e);
                }
            }
        }
        public static async Task Main(string[] args)
        {
            try
            {
                //=================================================================
                // Authenticate
                var clientId = Environment.GetEnvironmentVariable("CLIENT_ID");
                var clientSecret = Environment.GetEnvironmentVariable("CLIENT_SECRET");
                var tenantId = Environment.GetEnvironmentVariable("TENANT_ID");
                var subscription = Environment.GetEnvironmentVariable("SUBSCRIPTION_ID");
                ClientSecretCredential credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
                ArmClient client = new ArmClient(credential, subscription);

                await RunSample(client);
            }
            catch (Exception e)
            {
                Utilities.Log(e.ToString());
            }
        }
    }
}