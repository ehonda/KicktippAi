# Kicktipp Automation POC

This is a C# POC (Proof of Concept) that demonstrates automated login to kicktipp.de using HttpClient and AngleSharp, inspired by the Python [kicktipp-cli](https://github.com/ehonda/kicktipp-cli) project.

## Features

- ✅ Load credentials from `.env` file
- ✅ Automated login to kicktipp.de
- ✅ Session cookie extraction for future requests
- ✅ Clean service-based architecture
- ✅ Proper error handling and logging
- 🚧 Basic functionality ready for expansion

## Project Structure

```
src/Poc/
├── Program.cs              # Main entry point
├── Models/
│   └── KicktippModels.cs   # Data models (Match, Credentials)
├── Services/
│   └── KicktippService.cs  # Core service for kicktipp.de interaction
├── .env                    # Your credentials (not in version control)
├── .env.example           # Template for credentials
└── README.md              # This file
```

## Setup

1. **Install dependencies**: The required NuGet packages are already configured in the project file:
   - `AngleSharp` - For HTML parsing and DOM manipulation
   - `DotNetEnv` - For loading environment variables from .env file

2. **Create credentials file**: Copy `.env.example` to `.env` and fill in your kicktipp.de credentials:
   ```
   KICKTIPP_USERNAME=your_username_here
   KICKTIPP_PASSWORD=your_password_here
   ```

3. **Run the application**:
   ```bash
   dotnet run
   ```

## How it works

The POC implements the core login functionality by:

1. **Environment Loading**: Loads credentials from the `.env` file using `DotNetEnv`
2. **Service Creation**: Creates a `KicktippService` instance with HTTP client and cookie support
3. **Login Process**: 
   - Navigates to the kicktipp.de login page
   - Parses the login form using AngleSharp
   - Submits credentials (using field names `kennung` and `passwort` as identified in the Python reference)
   - Checks for successful login by analyzing the response
4. **Token Extraction**: Extracts the login cookie for future authenticated requests

## Architecture

The implementation follows clean architecture principles:
- **Program.cs**: Entry point and orchestration
- **Models/**: Data transfer objects (`Match`, `KicktippCredentials`)
- **Services/**: Business logic (`KicktippService`)

Key technical decisions:
- Uses HTTP client with session/cookie management (like Python `requests.Session`)
- Parses HTML using AngleSharp DOM library (equivalent to Python's BeautifulSoup)
- Implements IDisposable pattern for proper resource management
- Follows the same login flow as the Python reference implementation

## Next Steps

After successful login implementation, the next features to add would be:
- Community discovery and listing (`/info/profil/meinetipprunden`)
- Match parsing from tippabgabe pages (`/{community}/tippabgabe`)
- Bet placement functionality
- Prediction algorithms (SimplePredictor, CalculationPredictor equivalents)

## Security

- The `.env` file is excluded from version control via `.gitignore`
- Never commit credentials to the repository
- Use the `.env.example` file as a template

## Reference Implementation

This POC is based on the Python implementation at [ehonda/kicktipp-cli](https://github.com/ehonda/kicktipp-cli), specifically replicating the login functionality found in `kicktippbb.py`.
