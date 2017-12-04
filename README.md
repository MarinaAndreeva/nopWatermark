# nopWatermark - **image watermark plugin for NopCommerce**

[![GitHub Issues](https://img.shields.io/github/issues/MarinaAndreeva/nopWatermark.svg)](https://github.com/MarinaAndreeva/nopWatermark/issues)
![Contributions welcome](https://img.shields.io/badge/contributions-welcome-orange.svg) 
[![License: GPL v3](https://img.shields.io/badge/license-GPL%20v3-blue.svg)](https://www.gnu.org/licenses/gpl-3.0)

This repository contains Image Watermark Plugin for NopCommerce version 3.5, 3.6, 3.7, 3.8, 3.9, 4.0. This plugin dynamically adds watermarks (text or picture) to the store pictures without changing the original images. This plugin also supports a multi-store configuration.

## Table of contents

 - [Download](#download)
 - [Upgrade to the new version](#upgrade-to-the-new-version)
 - [Installation](#installation)
 - [Configuration](#configuration)
    - [Text watermark](#text-watermark)
    - [Picture watermark](#picture-watermark)
    - [Common settings](#common-settings)
    - [Multi-store configuration](#multi-store-configuration)
 - [Localization](#localization)
 - [Examples](#examples)
 - [Uninstallation](#uninstallation)
 - [Build Process](#build-process)
 - [Authors](#authors)
 - [License](#license)

## **Download**

You can download plugin from [marketplace](https://www.nopcommerce.com/p/2960/nopwatermark.aspx) or [github](https://github.com/MarinaAndreeva/nopWatermark/releases)

## **Upgrade to the new version**

### To upgrade a plugin:
- Uninstall the plugin from your store ([Plugin nopCommerce documentation](http://docs.nopcommerce.com/display/en/Plugins)).
- Upload the new version of the plugin to the /plugins folder in your nopCommerce directory.
- Restart your application (or click "Relod list of plugins" button).
- Scroll down through the list of plugins to find the newly installed plugin.
- Click on the Install link to install the plugin.
- The plugin is displayed in the Plugins windows (Configuration → Plugins → Local Plugins).

#### In nopCommerce 4.00 you can also:
- Go to the Plugins windows (Configuration → Plugins → Local Plugins).
- Uninstall the plugin.
- Delete the plugin (click "Delete" button).
- Click "Upload plugin" button.
- Select *zip* file that contains the new verion of the plugin and upload it.
- Click on the Install link to install the plugin.
- The plugin is displayed in the Plugins windows.

## **Installation**

### To install a plugin:
 - Upload the plugin to the /plugins folder in your nopCommerce directory.
 - Restart your application (or click "Relod list of plugins" button).
 - Scroll down through the list of plugins to find the newly installed plugin.
 - Click on the Install link to install the plugin.
 - The plugin is displayed in the Plugins windows (Configuration → Plugins → Local Plugins).

#### In nopCommerce 4.00 you can also:
- Go to the Plugins windows (Configuration → Plugins → Local Plugins).
- Click "Upload plugin" button.
- Select *zip* file that contains the plugin and upload it.
- Click on the Install link to install the plugin.
- The plugin is displayed in the Plugins windows.

### Find more: [Plugin nopCommerce documentation](http://docs.nopcommerce.com/display/en/Plugins)

## **Configuration**
 - Go to the “Administration -> Plugins -> Local Plugins”
 - Find “Image Watermark” in the plugin list.
 - Click “Configure”.
 
 ![Plugin configuration](https://user-images.githubusercontent.com/2384845/32978760-62fba478-cc51-11e7-9cc2-04886753a816.gif)
 
### **Text watermark**

This section contains a configuration for a text watermark.

![Text watermark](https://user-images.githubusercontent.com/2384845/32978765-75e8186e-cc51-11e7-8446-0161427ba0be.png)

Options:
 - **Enable text watermark** – check this box to enable text watermark.
 - **Text** – this text will be displayed in the store pictures.
 - You can also select: **text font**, **text color**, watermark **size in %** relative to the original image, **position** of the watermark text, **opacity** (0 – transparent text, 1 – opaque text) and watermark text **rotation angle**.

### **Picture watermark**

This section contains a configuration a picture watermark.

![Picture watermark](https://user-images.githubusercontent.com/2384845/32978767-7626726c-cc51-11e7-991f-13c16cd25127.png)

Options:
- **Enable picture watermark** – check this box to enable picture watermark.
- **Picture** – upload a watermark picture file.
- You can also select: watermark **size in %** relative to the original image, **position** of the watermark text, **opacity** (0 – transparent text, 1 – opaque text).

### **Common settings**

This section contains common configuration for a text/picture watermark.

![Common settings](https://user-images.githubusercontent.com/2384845/32978766-7606c52a-cc51-11e7-85f9-4c50f0915539.png)

Options:
- Tick the check boxes to select the sections to place the watermark: Products, Categories, Manufacturers.
- Enter the minimum image width/height to place the watermark. Watermarks will only be displayed on images that are larger/higher than the specified width/height. If width/height = 0, the watermark will be applied for all image sizes.

Finally click "Save".

### **Multi-store configuration**

Select the store in multi-store configuration list. Tick/untick check boxes to change configure the watermark values for the selected store.

![Multi-store configuration](https://user-images.githubusercontent.com/2384845/32978868-30d69974-cc53-11e7-993d-4030f1e87d6a.gif)

## **Localization**

The plugin is available in 3 languages: English, Russian, Ukrainian. 

You can add custom localization files to the "Resources" folder. The file name should look like this: **"Locale.seoCode.xml"**, where **seoCode** is two letter unique language code ([List of codes](https://geoffkenyon.com/google-iso-country-language-codes-international-seo/)).

To add a custom localization file:
- Open an existing localization file in a text editor.
- Change the "Value" property to the custom language value.
- Save file with file name **"Locale.seoCode.xml"**, where **seoCode** is two letter unique code for the added language ([List of codes](https://geoffkenyon.com/google-iso-country-language-codes-international-seo/)) in the "Resource" folder.
- Reinstall the plugin ([Plugin nopCommerce documentation](http://docs.nopcommerce.com/display/en/Plugins)).

## **Examples**

- Configuration plugin video: [click here](https://drive.google.com/file/d/1EfHKbuA8OXksk5y6gechQeP6LisjbU4q/view)
- Multi-store configuration plugin video [click here](https://drive.google.com/file/d/1Vxw7BukGIkRfUSaHbzN8rnzF9VhsJANd/view)

### Text watermark

![Text watermark](https://user-images.githubusercontent.com/2384845/32978771-83807156-cc51-11e7-8bbd-69a511062bd6.png)

![Product details page](https://user-images.githubusercontent.com/2384845/32978770-83604944-cc51-11e7-936a-a0bdf3eade1a.png)

### Picture watermark

![Picture watermark](https://user-images.githubusercontent.com/2384845/32978769-833dcf4a-cc51-11e7-8052-92df03f495a9.png)

![Products list](https://user-images.githubusercontent.com/2384845/32978772-83a08bd0-cc51-11e7-8cf4-9c01fb584e15.png)



## **Uninstallation**
Want to uninstall and revert back? Please feel free to open an issue regarding how we can improve this plugin.

To uninstall plugin:
- Go to Configuration → Plugins → Local Plugins.
- Click the Uninstall link beside the Image Watermark plugin to uninstall. The plugin is uninstalled. The link text in the Installation column changes to "Install" enabling you to reinstall the plugin at any time.

## **Build process**

- Clone this repository into <code>nopCommerce/src/Plugins</code> folder (<code>git clone https://github.com/MarinaAndreeva/nopWatermark.git</code>)
- Add <code>Nop.Plugin.Misc.Watermark.csproj</code> to nopCommerce solution as existing project into "Plugins" folder.
- Build the project, plugin's files will be placed in the <code>nopCommerce/src/Presentation/Nop.Web/Plugins/**Misc.Watermark**</code> folder.

## **Authors**

- Marina Andreeva - [github](https://github.com/MarinaAndreeva)

## **License**

The Watermark Plugin for nopCommerce is licensed under the terms of the GPL v3 Open Source license and is available for free for commercial and personal usage.
