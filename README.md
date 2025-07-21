# KicktippAi - Kicktipp.de Automation POC

A C# proof-of-concept for automating login and betting on kicktipp.de, inspired by the Python project [schwalle/kicktipp-betbot](https://github.com/schwalle/kicktipp-betbot) and its fork [ehonda/kicktipp-cli](https://github.com/ehonda/kicktipp-cli).

## Features

✅ **Completed:**
- **Environment-based credentials**: Load username/password from `.env` file
- **Secure login automation**: Uses HttpClient and AngleSharp for form parsing and submission
- **Cookie management**: Extracts and saves login tokens for future use
- **Open predictions fetching**: Retrieves available matches for betting
- **Random bet placement**: Automatically places random bets on open predictions
- **Safety features**: Dry-run mode and existing bet detection
- **Hard-coded community**: Currently targets "ehonda-test" community

## Architecture

- **HttpClient + AngleSharp**: Modern C# web automation stack
- **Environment variables**: Secure credential storage via `.env` file
- **HTTPS support**: All communication uses secure connections
- **Form parsing**: Robust HTML form handling for login and betting
- **Error handling**: Comprehensive exception handling and status reporting

## Usage

### Setup Credentials

For security, credentials are stored outside the repository to prevent AI agents with solution directory read access from accidentally leaking credentials to remote sources:

1. **Create secrets directory**: A `KicktippAi.Secrets` directory should exist as a sibling to the solution directory
2. **Setup credentials**: Copy `.env.example` to the secrets directory at `KicktippAi.Secrets/dev/Poc/.env`
3. **Add your credentials**: Edit the `.env` file with your actual kicktipp.de credentials

```
# Directory structure:
├── KicktippAi/                 # This repository
│   ├── dev/Poc/.env.example    # Template file
│   └── ...
└── KicktippAi.Secrets/         # Secrets directory (outside repo)
    └── dev/Poc/.env            # Your actual credentials
```

### Running the Application

1. **Navigate to project**: `cd dev/Poc`
2. **Run the application**: `dotnet run`
3. **Interactive betting**: The app will show a dry-run first, then ask for confirmation

## Example Output

```
Kicktipp.de Automation POC
==========================
✓ Login successful!
✓ Login token extracted and saved to .env file

✓ Found 4 open matches:
  04.07.2025 21:00 'Fluminense' vs. 'Al-Hilal'
  05.07.2025 03:00 'Palmeiras' vs. 'FC Chelsea'
  05.07.2025 18:00 'Paris St. Germain' vs. 'FC Bayern München'
  05.07.2025 22:00 'Real Madrid' vs. 'Borussia Dortmund'

=== DRY RUN ===
05.07.2025 03:00 'Palmeiras' vs. 'FC Chelsea' - betting 3:0
05.07.2025 18:00 'Paris St. Germain' vs. 'FC Bayern München' - betting 0:0
05.07.2025 22:00 'Real Madrid' vs. 'Borussia Dortmund' - betting 1:2
Summary: 3 bets to place, 0 skipped

Do you want to place these bets for real? (y/N): y

=== PLACING REAL BETS ===
✓ Successfully submitted 3 bets!
```

## Technical Implementation

### Random Bet Generation
The `SimplePredictor` class generates random but realistic football scores:
- Common scores like 1:0, 2:1, 1:1, 3:1, etc.
- Based on the Python reference implementation patterns

### Form Field Detection
- Automatically finds betting input fields ending with `_heimTipp` and `_gastTipp`
- Handles hidden form fields and submit buttons correctly
- Follows the same patterns as the Python kicktipp-cli

### Safety Features
- **Dry run mode**: Shows what would be bet without actually submitting
- **Existing bet detection**: Skips matches where bets are already placed
- **Override option**: Can override existing bets if needed (implemented but not exposed in UI)
- **Interactive confirmation**: User must explicitly confirm bet placement

## Dependencies

- **.NET 9.0**: Modern C# runtime
- **AngleSharp**: HTML parsing and DOM manipulation
- **DotNetEnv**: Environment variable loading from `.env` files

## Project Structure

```
dev/Poc/
├── Program.cs              # Main application entry point
├── Services/
│   └── KicktippService.cs  # Core web automation logic
├── Models/
│   └── KicktippModels.cs   # Data models and predictor logic
└── .env.example            # Environment template (instructions only)

KicktippAi.Secrets/         # External secrets directory
└── dev/Poc/
    ├── .env.example        # Copy of environment template  
    └── .env                # Your actual credentials (gitignored)
```

## Future Enhancements

Potential next steps:
- Multi-community support (beyond hardcoded "ehonda-test")
- More sophisticated prediction algorithms
- Command-line arguments for configuration
- Scheduling and automation features
- Better error handling and retry logic

## Acknowledgments

This project is inspired by and follows the patterns established in [schwalle/kicktipp-betbot](https://github.com/schwalle/kicktipp-betbot), the original Python implementation for kicktipp.de automation. We specifically used the [ehonda/kicktipp-cli](https://github.com/ehonda/kicktipp-cli) fork (dev branch) as a reference for translating the Python implementation concepts to modern C#.
