# Blackbird.io Microsoft OneDrive

Blackbird is the new automation backbone for the language technology industry. Blackbird provides enterprise-scale automation and orchestration with a simple no-code/low-code platform. Blackbird enables ambitious organizations to identify, vet and automate as many processes as possible. Not just localization workflows, but any business and IT process. This repository represents an application that is deployable on Blackbird and usable inside the workflow editor.

## Introduction

<!-- begin docs -->

OneDrive is the Microsoft cloud service that connects you to all your files. It lets you store and protect your files, share them with others, and get to them from anywhere on all your devices.

## Connecting

1. Navigate to apps and search for Microsoft OneDrive.
2. Click _Add Connection_.
3. Name your connection for future reference e.g. 'My organization'.
4. Click _Authorize connection_.
5. Follow the instructions that Microsoft gives you, authorizing Blackbird.io to act on your behalf.
6. When you return to Blackbird, confirm that the connection has appeared and the status is _Connected_.

## Actions

### Files

- **Upload file** Upload file to specified folder
- **Download file** Download specified file
- **Search files** List files metadata in specified folder
- **Get file metadata** Get information about a specific file
- **Delete file** Delete specified file

### Folders

- **Search folder** Find a folder by name
- **Get folder metadata** Get information about a specific folder
- **Create folder** Create a new folder in another folder
- **Delete folder** Delete specified folder

> When uploading a file to a folder, you can choose from the following options how to behave in case of conflict (there is already a file with the same name in the specified folder):
*Fail Uploading*: If a file with the same name exists, the upload will stop, preserving the original file.
*Replace File*: The existing file will be overwritten by the new upload, replacing its contents.
*Rename File*: The new file will be saved with a unique name, preserving both the original and the uploaded files.

## Events

### Files

- **On files updated** This polling event is triggered when files are created or updated. You can specify the folder to watch for changes and `Include subfolders` option to include changes in subfolders (by default it is set to `false`).

### Folders
- **On folders updated** This polling event is triggered when folders are created or updated.

## Examples

![Connecting](image/README/example_bird.png)<br>
This bird fetches new or updated files from OneDrive, translates them with DeepL and then sends them to Slack channel

## Feedback

Do you want to use this app or do you have feedback on our implementation? Reach out to us using the [established channels](https://www.blackbird.io/) or create an issue.

<!-- end docs -->
