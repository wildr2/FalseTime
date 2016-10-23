
import math
import sys


def f(x):
	arrive = 0
	prev = 0
	i = 0
	while True:
		current = x+arrive
		if current == prev: break
		print 'pass',i,':', current,'send',math.ceil(current/2.0),
		print 'fwd',math.ceil(current/4.0)
		arrive = math.ceil(current/4.0)
		prev = current
		i += 1

f(float(sys.argv[1])) 