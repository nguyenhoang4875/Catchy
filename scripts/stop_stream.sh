#!/bin/bash

# Check if the PID file exists
if [ -f "C:/QtLogViewer/stream_pid.txt" ]; then
    # Read the PID of the streaming process
    STREAM_PID=$(cat "C:/QtLogViewer/stream_pid.txt")
    
    # Kill the process
    kill $STREAM_PID
    
    # Remove the PID file
    rm "C:/QtLogViewer/stream_pid.txt"
    
    echo "Streaming stopped."
else
    echo "No streaming process found."
fi