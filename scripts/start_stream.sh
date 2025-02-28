#!/bin/bash
# # Create askpass.sh script
# cat >askpass.sh <<EOF
# echo -n root
# EOF

# Make it executable
chmod 0755 askpass.sh

# Variables
REMOTE_HOST=$1  # Remote host and user
REMOTE_FILE=$2  # Path to the file on the remote host
LOCAL_FILE=$3  # Path to the local file
SSH_GATEWAY=$4
SSH_GATEWAY_PORT=$5

echo "REMOTE_HOST: $REMOTE_HOST"
echo "REMOTE_FILE: $REMOTE_FILE"
echo "LOCAL_FILE: $LOCAL_FILE"
echo "SSH_GATEWAY: $SSH_GATEWAY"
echo "SSH_GATEWAY_PORT: $SSH_GATEWAY_PORT"


mkdir -p $(dirname $LOCAL_FILE)

# Stream the file in real-time
env \
  SSH_ASKPASS=./askpass.sh \
  SSH_ASKPASS_REQUIRE=force \
ssh -v \
  -o StrictHostkeyChecking=no \
  -o HostkeyAlgorithms=+ssh-rsa \
  -o "ProxyCommand ssh -o HostkeyAlgorithms=+ssh-rsa -o StrictHostKeyChecking=no -q -p $SSH_GATEWAY_PORT $SSH_GATEWAY nc %h %p" \
  $REMOTE_HOST \
  "tail -f $REMOTE_FILE" >> $LOCAL_FILE &
#ssh -o StrictHostKeyChecking=no -o "ProxyCommand ssh -o StrictHostKeyChecking=no -q -p $SSH_GATEWAY_PORT $SSH_GATEWAY nc %h %p" $REMOTE_HOST "tail -f /host/log/messages" >> $LOCAL_FILE &
  SSH_PID=$!
  echo $SSH_PID > C:/QtLogViewer/stream_pid.txt
echo "Streaming started..."
# read -p "Press any key to stop the stream..."