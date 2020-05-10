#!/usr/bin/env python3
import subprocess
import argparse
import re

class App:
	pass

def run_cmd(cmd):
	p = subprocess.run(cmd, shell=True, encoding='utf-8', stdout=subprocess.PIPE, stderr=subprocess.PIPE)
	return p.stdout, p.stderr, p.returncode

def get_args():
	parser = argparse.ArgumentParser(description='Xpra App Manager')
	parser.add_argument('--show', action='store_true', help='list apps')
	parser.add_argument('--run', help='run app')
	args = parser.parse_args()
	return args

def show():
	cmd = 'ps aux |grep -v grep |grep "/bin/xpra"'
	o,e,r = run_cmd(cmd)
	if o:
		for line in o.split('\n'):
			m = re.match(r'.+/bin/xpra start :([0-9]+) .+--start-child=(.+) .+', line)
			if m:
				print(m.group(1), m.group(2))




def main():
	a = get_args()
	if a.show:
		show()
	elif a.run:
		run(a.run)


if __name__ == '__main__':
	main()