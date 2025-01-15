#!/bin/bash
LOG_FILE="/home/gpm/compressHistory.log"
TARGET_DIR="/home/gpm/GPM_AGV_LOG"
yesterday=$(date -d 'yesterday' +%Y-%m-%d)
TARGET_LOG_FOLDER=$TARGET_DIR/$yesterday
TARGET_Compressd_FILE=$TARGET_LOG_FOLDER.tar.gz

if [ -d "$TARGET_LOG_FOLDER" ]; then
    echo "[$(date)] Start Compress LOG to ${TARGET_Compressd_FILE}" >> "$LOG_FILE"
    
    tar -czvf ${TARGET_Compressd_FILE} -C ${TARGET_DIR} $yesterday
    echo "[$(date)] Compress Log folder() to ${TARGET_Compressd_FILE} COMPLETED" >> "$LOG_FILE"
    if [ $? -eq 0 ]; then
        echo "[$(date)] Compress LOG to ${TARGET_Compressd_FILE}" >> "$LOG_FILE"
        rm -rf "$TARGET_DIR/$yesterday"
        if [ -d "$TARGET_DIR/$yesterday" ]; then
            echo "[$(date)] Delete "$TARGET_LOG_FOLDER" Folder Fail.." >> "$LOG_FILE"
        else
            echo "[$(date)] Delete "$TARGET_LOG_FOLDER" Folder Success!" >> "$LOG_FILE"
        fi
    else
        echo "[$(date)] Compress LOG to ${TARGET_Compressd_FILE} FAIL" >> "$LOG_FILE"
    fi
else
    echo "[$(date)] Folder not exist:$TARGET_LOG_FOLDER" >> "$LOG_FILE"	
fi

