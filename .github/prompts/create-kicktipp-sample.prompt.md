---
mode: agent
---
# Create Kicktipp Sample

⚠️ *Obsolete* ⚠️ - This prompt is no longer used. It has been replaced by the `Create-KicktippSample.ps1` script in
the root directory. It's only kept here for reference and education purposes.

## Goal

This is a simple two step workflow to create a sample (snapshot at a specific point in time) of a Kicktipp page. The
goal is to create a file with a standardized name in a standardized location, where we can then copy pasted the content
of a Kicktipp page into.

## Procedure

* Await page name input from the user
* Create a suffix for the file name based on the current date and time (UTC), in the format `yyyy-MM-dd-HH-mm-ss`
  * 💡 Use powershell's `Get-Date` to get the current date and time in UTC
* Create an empty file with the name `kicktipp-samples/{pageName}/{pageName}-{suffix}.html`
