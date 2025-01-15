#!/bin/bash
LOG_DIR="/home/gpm/FileClearLogs"
TARGET_DIR="/home/gpm/GPM_AGV_LOG"
Days=90
mkdir -p $LOG_DIR
LOG_FILE="$LOG_DIR/cleanup_$(date +'%Y-%m-%d').log"

sudo chmod -R 777  $TARGET_DIR
# Log the start of the cleanup
echo "Cleanup Files Older than $Days ago started at $(date)" >> "$LOG_FILE"

#delete file which older than 90 days
find "$TARGET_DIR" -type f -mtime +"$Days" -exec rm -f {} \; -exec echo "Deleted: {}" >> "$LOG_FILE" \;

#delete empty directory
find "$TARGET_DIR" -type d -empty -exec rmdir {} \; -exec echo "Removed empty directory: {}" >> "$LOG_FILE" \;

# Log the completion of the cleanup
echo "Cleanup Files Older than $Days ago completed at $(date)" >> "$LOG_FILE"
echo "Deleted files older than $Days days ago from $TARGET_DIR COMPLETED."

