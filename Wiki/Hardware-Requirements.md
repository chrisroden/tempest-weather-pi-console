# Required Hardware

## Required: Tempest Weather System

This app requires a WeatherFlow Tempest system as its upstream data source.

What you need from Tempest:

- Tempest outdoor sensor device
- Tempest Hub
- Tempest account and station configured in the Tempest app
- API token and station metadata for this app configuration

Official product and support links:

- Tempest Store product page: https://shop.tempest.earth/products/tempest
- Tempest Support (Getting Started): https://help.tempest.earth/hc/en-us/sections/204287028-Getting-Started
- Tempest Quick Start Guide: https://help.tempest.earth/hc/en-us/articles/360047221633-Tempest-Quick-Start-Guide
- Tempest Siting and Installation: https://help.tempest.earth/hc/en-us/articles/115005229767-Siting-Installation-for-Tempest
- Tempest technical details (power/battery article): https://help.tempest.earth/hc/en-us/articles/360048877194-Solar-Power-Rechargeable-Battery

Amazon link (US marketplace):

- Tempest Weather Station listing: https://www.amazon.com/Tempest-Weather-Accurate-Forecasts-Wireless/dp/B0868WY7NY

Notes:

- Amazon availability, seller, and pricing vary by region and date.
- For highest confidence, verify model details against the official Tempest store/support pages.

## Raspberry Pi Recommendations

- Raspberry Pi 4 or 5 recommended
- Raspberry Pi OS Desktop for full UI mode
- Raspberry Pi OS Lite for backend-only/headless mode
- 16GB microSD minimum (32GB recommended)
- Stable power supply and network connectivity

## Display Recommendations (for UI mode)

- Recommended: ROADOM 10.1-inch touchscreen display (1024x600)
- Amazon link: https://www.amazon.com/dp/B09XDK2FRR
- Touchscreen optional but recommended
- Kiosk placement with continuous power

## Network and Services

- Local backend listens on port `5000` by default
- UI and backend normally run on same Pi (`localhost`)
- Backend needs outbound internet access to Tempest services
