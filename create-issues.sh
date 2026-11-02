#!/usr/bin/env bash
set -euo pipefail

REPO="CL-14/Dotnet_TFI_Project"

# Create the labels. --force updates a label if it already exists.
gh label create "user-story" --repo "$REPO" --color 1D76DB --description "User story" --force
gh label create "must"       --repo "$REPO" --color B60205 --description "Core prototype" --force
gh label create "should"     --repo "$REPO" --color FBCA04 --description "Wanted if time allows" --force
gh label create "stretch"    --repo "$REPO" --color 0E8A16 --description "Future work" --force

# Reads each line as: id | title | story | priority.
# The lowercase priority is reused as the label name.
while IFS='|' read -r id title story prio; do
  [ -z "$id" ] && continue
  gh issue create --repo "$REPO" \
    --title "$id: $title" \
    --body "$(printf '%s\n\n**Priority:** %s' "$story" "$prio")" \
    --label "user-story" --label "${prio,,}"
done <<'EOF'
US1|Live bus map|As a commuter, I want to see live bus positions on a map so that I can see where my bus actually is.|Must
US2|Stop search|As a commuter, I want to search for a stop by name or code so that I can find mine quickly.|Must
US3|Nearby buses at a stop|As a commuter, I want to see buses near a stop within a chosen radius so that I can judge how soon one will arrive.|Must
US4|Upcoming arrivals list|As a commuter, I want a clean list of upcoming arrivals at a stop so that I can plan without clutter.|Must
US5|Delay information|As a commuter, I want to see how late or early a bus is so that I can decide whether to wait.|Should
US6|Route reliability indicator|As a commuter, I want a reliability indicator for each route so that I know how far to trust its estimates.|Must
US7|Service alerts|As a commuter, I want to see active alerts (diversions, roadworks) for my route or stop so that I'm not surprised.|Must
US8|Filter map by route|As a commuter, I want to filter the map by route so that I only see the buses I care about.|Should
US9|Favourite stops|As a commuter, I want to save favourite stops so that I can reach them quickly.|Should
US10|Mobile-friendly layout|As a commuter, I want the app to work well on my phone so that I can use it at the bus stop.|Should
US11|Data unavailable message|As a commuter, I want a clear message when live data is unavailable so that I don't rely on stale information.|Must
US12|Use without registering|As a visitor, I want to use the app without registering so that I can check a bus in seconds.|Must
US13|Stop name and location|As a visitor, I want to see a stop's name and location on the map so that I know where to stand.|Should
US14|Plain-language explanations|As a visitor, I want plain-language explanations of terms like "ghost bus" and "delay" so that I understand the reliability figures.|Stretch
US15|Reliability by route over time|As an analyst, I want to view reliability by route over a date range so that I can see trends.|Should
US16|Reliability by time of day|As an analyst, I want to view reliability by time of day so that I can see when service is worst.|Should
US17|CSV export|As an analyst, I want to export reliability data as CSV so that I can analyse it further.|Stretch
US18|Compare with NTA figures|As an analyst, I want to compare the app's measure with the NTA's published figures so that I can judge its validity.|Stretch
US19|Continuous data collector|As an administrator, I want the collector to run continuously within the API limit so that the dataset has no gaps.|Must
US20|Collector status|As an administrator, I want to see the collector's status and last successful fetch so that I can spot outages.|Should
US21|API error handling|As an administrator, I want clear errors when the API fails (rate limit, bad key) so that I can fix problems fast.|Must
US22|Refresh static schedule data|As an administrator, I want to refresh the static schedule data when the NTA publishes updates so that schedules stay correct.|Should
US23|Login and roles|As an administrator, I want login with role-based access so that only authorised users reach admin pages.|Should
US24|Rate a route|As a commuter, I want to rate a route (for example, how crowded it is) so that other passengers see community feedback.|Stretch
US25|Route recommendations|As a commuter, I want recommended routes between two places so that I can plan a journey.|Stretch
US26|Report a missed bus|As a commuter, I want to report a missed bus so that these reports can be collected and shared with the NTA.|Stretch
US27|NTA data attribution|As a user, I want to see that the data comes from the NTA and is provided "as is" so that its source and limits are clear.|Must
EOF
