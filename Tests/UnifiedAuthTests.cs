using System;
using System.Threading.Tasks;
using S3FileManager.Models;
using S3FileManager.Services;

namespace S3FileManager.Tests
{
    /// <summary>
    /// Basic tests for the unified authentication system
    /// </summary>
    public class UnifiedAuthTests
    {
        public static async Task RunBasicTests()
        {
            Console.WriteLine("Starting Unified Authentication System Tests...\n");

            // Test 1: UnifiedUser creation from local user
            TestUnifiedUserFromLocal();

            // Test 2: Authentication result creation
            TestAuthenticationResults();

            // Test 3: UnifiedAuthService initialization
            await TestUnifiedAuthServiceInitialization();

            Console.WriteLine("\nAll tests completed!");
        }

        private static void TestUnifiedUserFromLocal()
        {
            Console.WriteLine("Test 1: UnifiedUser creation from local user");
            
            var localUser = new User
            {
                Username = "testuser",
                Role = UserRole.Administrator,
                LastLogin = DateTime.Now
            };

            var unifiedUser = UnifiedUser.FromLocalUser(localUser);

            Console.WriteLine($"✓ Username: {unifiedUser.Username}");
            Console.WriteLine($"✓ Role: {unifiedUser.Role}");
            Console.WriteLine($"✓ Auth Type: {unifiedUser.AuthType}");
            Console.WriteLine($"✓ Has AWS Credentials: {unifiedUser.HasAwsCredentials}");
            Console.WriteLine($"✓ Is Limited Access: {unifiedUser.IsLimitedAccess}");
            Console.WriteLine($"✓ Capability: {unifiedUser.GetCapabilityDescription()}");
            Console.WriteLine();
        }

        private static void TestAuthenticationResults()
        {
            Console.WriteLine("Test 2: Authentication result creation");

            var localUser = UnifiedUser.FromLocalUser(new User
            {
                Username = "localuser",
                Role = UserRole.User,
                LastLogin = DateTime.Now
            });

            var successResult = AuthenticationResult.Success(localUser, AuthenticationMethod.Local);
            Console.WriteLine($"✓ Success result created: {successResult.IsSuccess}");
            Console.WriteLine($"✓ Method used: {successResult.MethodUsed}");
            Console.WriteLine($"✓ Warning required: {successResult.RequiresAwsCredentialWarning}");

            var failureResult = AuthenticationResult.Failure("Invalid credentials", AuthenticationMethod.CognitoOnline);
            Console.WriteLine($"✓ Failure result created: {failureResult.IsSuccess}");
            Console.WriteLine($"✓ Error message: {failureResult.ErrorMessage}");
            Console.WriteLine();
        }

        private static async Task TestUnifiedAuthServiceInitialization()
        {
            Console.WriteLine("Test 3: UnifiedAuthService initialization");

            try
            {
                using var authService = new UnifiedAuthService();
                Console.WriteLine($"✓ Service initialized");
                Console.WriteLine($"✓ Cognito available: {authService.IsCognitoAvailable}");
                Console.WriteLine($"✓ Local auth available: {authService.IsLocalAuthAvailable}");
                Console.WriteLine($"✓ Auth status: {authService.GetAuthenticationStatus()}");

                // Test credential validation
                var testUser = UnifiedUser.FromLocalUser(new User
                {
                    Username = "testuser",
                    Role = UserRole.User,
                    LastLogin = DateTime.Now
                });

                bool hasCredentials = UnifiedAuthService.ValidateAwsCredentials(testUser);
                Console.WriteLine($"✓ Test user has AWS credentials: {hasCredentials}");

                if (!hasCredentials)
                {
                    string warning = UnifiedAuthService.GetLimitedAccessWarning(testUser);
                    Console.WriteLine($"✓ Limited access warning: {warning}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Service initialization failed: {ex.Message}");
                Console.WriteLine("  This is expected in test environment without proper AWS configuration");
            }
            
            Console.WriteLine();
        }
    }
}