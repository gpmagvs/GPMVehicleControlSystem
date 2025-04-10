#!/bin/bash

echo "Restart gpm.launch ..."
pkill -f "roslaunch agv_control_settings gpm.launch"
sleep 1

source /opt/ros/kinetic/setup.bash
source /opt/ros/noetic/setup.bash
source ~/catkin_ws/devel/setup.bash
echo 12345678 | gnome-terminal "agvc" --window -e "sleep 1"\
  --tab -e 'bash -c "sleep 1; roslaunch agv_control_settings gpm.launch ;exec bash"'\
  
