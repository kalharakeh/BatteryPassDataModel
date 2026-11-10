# Changelog
All notable changes to the battery passport data model will be documented in this file.

## Wave 1: security fixes (10 Nov 2026)

Fixes from the review of the app on tag `new-app-v3.2`. The finding IDs are in brackets.

### For the team

- A cluster admin can no longer take over an account. "Add user" with an address that already has an account only adds it to their cluster; they change a name or password only for people who are in their clusters alone, have no role across the app and were made by a cluster admin of theirs [R19, S04, S08].
- The team's "Add user" refuses an address that has an account: change it from its row. Giving an account a role across the app needs a new password in the same step. Nobody can lower or delete their own account, and the last team account can't be lowered or deleted [R19, S04].
- A membership is a cluster user or a cluster admin, nothing else [S08].
- A new password has at least 12 characters. The account page asks for the current password before it changes the password or the e-mail [S06].
- An e-mail address is the same whatever its capitals [U28].
- Deleting an account, lowering it, removing it from a cluster or changing its password ends its open sessions on their next page [S02].
- The fallback admin has no password in the code any more. In Production the app doesn't start with it switched on; elsewhere it works only with a password set in the environment [R21].
- The team can withdraw an accident report with a reason. Its file is deleted; the audit log keeps who, when, why and the file's name [S07].
- The team can undo a waste, with a reason: the battery goes back to the status of the version before [R10].
- Demo data can be switched off with `ENABLE_DEMO_DATA` (off in Production unless set). A real battery ID can't be created or first published on demo product data, and an ID can only be swapped for one of the same kind [R03, U16].
- File actions (accident reports, "Check the file") share the upload rate limit [S07].
- Only http and https addresses become links [S01]. The accident link (#92) belongs to the app and can't be set by a file; "Check the file" shows a current value only to a sender who may see it [S03, S05].

### For cluster admins

- "Add user" with an address that already has an account only adds the person to your cluster; their name and password stay as they are. For people outside your clusters the page says "You can't change this person's account here. Ask the Scania team." [R19]
- The x beside a cluster asks first, then removes the person, and says so afterwards [U29].
- "Waste" asks a second question, because only the team can undo it [R10].
- The account page says "Your name", "Your e-mail" and "Current password", and a cluster user's role reads "Cluster member" [W04, S06].

### For readers

- Market surveillance marks a passport invalid with a button in the Purchaser card, then a confirm step [U11].
- A signed-in reader who opens the login address goes to their start page, and the login page says how to get an account [U31, U03].
- Demo accounts and demo tokens reach only demo batteries; on any other battery they see what a visitor sees [R03, U16].

## Battery ID and passport snapshots

- Added first-class Battery IDs and Passport IDs.
- Moved management, registry, QR, telemetry, and external battery API routes to Battery ID.
- Kept passports as immutable non-telemetry snapshots with historical/latest state.
- Renamed user-facing Battery version to Battery Model.

## [1.2.0] - 2025-10-23
 - Lincense consistently updated to Creative Commons Attribution 4.0 International (CC-BY-4.0) license. See the [LICENSE](https://creativecommons.org/licenses/by/4.0/legalcode) file for more details. 

## [1.2.0] - 2025-01-14
### Updated
- Updated battery passport data model according to: 
  - ### DIN DKE Spec 99100:2025-02 "Requirements for data attributes of the battery passport"
- Link to the DIN Spec: https://www.dinmedia.de/en/technical-rule/din-dke-spec-99100/385692321
  

## [1.0.0] - 2024-05-28
### New
- Initial battery passport data model




