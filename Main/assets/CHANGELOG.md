# v1.2.0
- Splitting the project to attempt to deal with issues of uploading on ThunderStore
- Properly setting up endec
- Rework API to be more convenient for others to use within the future
- Removing unneeded files
- Fixing issues with the latest release of the game
- Add tooltip info combined with dump commands, keybinds, and more
- Rename config values
- Adjust some settings to be user based to allow for more control when using modpacks

# v1.1.7
- Make Local rating optional in data format
- Update method on getting extension information from URL if unable to query data though URL using head
- Fix issues with internal image querys for Missing, Error, Loading and Censored being broken due to local rework in v1.1.3
- Add log for if internal image querys were not found during loading to better indicate such issue if it occurs

## v1.1.6
- Fix issues with local image files being improperly handled for their identifier and adjust file name for user settings

## v1.1.5
- Fix issues related to local queries having duplicate names without unique taking into the folder/mod as namespace
- Revamp global user settings access for tag mangement and users app credentials

## v1.1.4
- Refactor around Media Info to have more optional fields and add MediaFormat Endec
- Add ability to toggle off global blacklist and whitelist tags

## v1.1.3
- Prevent duplicate caching of local media results
- Fix issues with crashing due to off thread actions, refactor URI -> Identifier code for future plans
- Fix local media file regex
- Rework Web Queries to be loaded as separate plugin and patch reddit links to work
- Fix issues with Web Media Query and add more checks for areas that cause possible exceptions

## v1.1.2
- Fix issues where blacklist is only applying in restrictiveQueries mode
- Add more sanitization steps and prevent ability for path traversel

## v1.1.1
- Add ability for local queries to have media rating

## v1.1.0
- Initial Release